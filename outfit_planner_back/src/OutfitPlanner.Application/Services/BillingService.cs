using OutfitPlanner.Application.Abstractions;
using OutfitPlanner.Domain;

namespace OutfitPlanner.Application.Services;

public sealed record BillingSubscriptionInfo(string Status, DateTimeOffset? CurrentPeriodEnd, bool PremiumActive);

public sealed record BillingTopUpPackInfo(string Id, int Credits, string? DisplayPrice);

public sealed record BillingStatus(
    bool Enabled,
    string Provider,
    bool SubscriptionPriceConfigured,
    string? PremiumDisplayPrice,
    BillingSubscriptionInfo? Subscription,
    IReadOnlyList<BillingTopUpPackInfo> TopUpPacks,
    bool PortalAvailable);

public sealed record BillingWebhookResult(string Status)
{
    public static BillingWebhookResult Processed { get; } = new("processed");
    public static BillingWebhookResult Duplicate { get; } = new("duplicate");
    public static BillingWebhookResult Ignored { get; } = new("ignored");
}

// Stage-4 billing use cases (PAYWALL_MODEL.md): checkout/portal delegation to the
// configured provider, and idempotent webhook handling that mirrors subscription state
// and flips stored Free ↔ Premium roles. Deviation from the original sketch: webhooks
// never write credit-grant rows — the lazy monthly grant in CreditLedgerService stays
// the single granting authority, driven by the effective role this service flips.
public sealed class BillingService
{
    private readonly IUserAccountRepository _users;
    private readonly ISubscriptionRepository _subscriptions;
    private readonly IBillingEventRepository _events;
    private readonly CreditLedgerService _credits;
    private readonly IBillingProvider _provider;
    private readonly BillingOptions _options;
    private readonly RolePinningPolicy _rolePinning;
    private readonly IClock _clock;

    public BillingService(
        IUserAccountRepository users,
        ISubscriptionRepository subscriptions,
        IBillingEventRepository events,
        CreditLedgerService credits,
        IBillingProvider provider,
        BillingOptions options,
        RolePinningPolicy rolePinning,
        IClock clock)
    {
        _users = users;
        _subscriptions = subscriptions;
        _events = events;
        _credits = credits;
        _provider = provider;
        _options = options;
        _rolePinning = rolePinning;
        _clock = clock;
    }

    public BillingStatus GetStatus(string userId)
    {
        var subscription = _subscriptions.GetSubscriptionByUser(userId);
        var info = subscription is null
            ? null
            : new BillingSubscriptionInfo(
                subscription.Status,
                subscription.CurrentPeriodEnd,
                BillingRules.GrantsPremium(subscription.Status));
        return new BillingStatus(
            _provider.Enabled,
            _provider.Name,
            _options.PremiumPriceId.Length > 0,
            _options.PremiumDisplayPrice,
            info,
            OfferedPacks().Select(pack => new BillingTopUpPackInfo(pack.Id, pack.Credits, pack.DisplayPrice)).ToArray(),
            subscription is not null);
    }

    public Task<string> StartSubscriptionCheckoutAsync(string userId, CancellationToken cancellationToken)
    {
        RequireEnabled();
        if (_options.PremiumPriceId.Length == 0)
        {
            throw new ValidationException("The Premium subscription price is not configured.");
        }

        var user = RequireUser(userId);
        switch (_rolePinning.EffectiveRole(user))
        {
            case UserRole.Admin:
                throw new ValidationException("Admin accounts do not need a subscription.");
            case UserRole.Premium:
                throw new ValidationException("You already have Premium. Manage your subscription from the billing portal.");
        }

        return _provider.CreateSubscriptionCheckoutAsync(
            user,
            _options.PremiumPriceId,
            _options.CheckoutSuccessUrl,
            _options.CheckoutCancelUrl,
            cancellationToken);
    }

    public Task<string> StartTopUpCheckoutAsync(string userId, string packId, CancellationToken cancellationToken)
    {
        RequireEnabled();
        var user = RequireUser(userId);
        switch (_rolePinning.EffectiveRole(user))
        {
            case UserRole.Admin:
                throw new ValidationException("Admin accounts have unlimited credits.");
            case UserRole.Free:
                throw new ValidationException("Credit top-ups are part of the Premium plan. Upgrade first.");
        }

        var pack = OfferedPacks().FirstOrDefault(candidate =>
            string.Equals(candidate.Id, packId?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new ValidationException("Unknown top-up pack.");
        return _provider.CreateTopUpCheckoutAsync(
            user,
            pack,
            _options.CheckoutSuccessUrl,
            _options.CheckoutCancelUrl,
            cancellationToken);
    }

    public Task<string> CreatePortalAsync(string userId, CancellationToken cancellationToken)
    {
        RequireEnabled();
        RequireUser(userId);
        var subscription = _subscriptions.GetSubscriptionByUser(userId)
            ?? throw new ValidationException("No subscription to manage.");
        return _provider.CreatePortalSessionAsync(
            subscription.ExternalCustomerId,
            _options.PortalReturnUrl,
            cancellationToken);
    }

    public Task<BillingWebhookResult> HandleWebhookAsync(string payload, string? signatureHeader, CancellationToken cancellationToken)
    {
        RequireEnabled();
        var webhookEvent = _provider.ParseWebhookEvent(payload, signatureHeader)
            ?? throw new ValidationException("Invalid webhook signature.");
        if (!_events.TryRecordBillingEvent(webhookEvent.EventId, _clock.UtcNow))
        {
            return Task.FromResult(BillingWebhookResult.Duplicate);
        }

        var result = webhookEvent.Kind switch
        {
            BillingWebhookEventKind.CheckoutCompleted => HandleCheckoutCompleted(webhookEvent),
            BillingWebhookEventKind.SubscriptionUpdated => HandleSubscriptionChanged(webhookEvent, deleted: false),
            BillingWebhookEventKind.SubscriptionDeleted => HandleSubscriptionChanged(webhookEvent, deleted: true),
            _ => BillingWebhookResult.Ignored
        };
        return Task.FromResult(result);
    }

    private BillingWebhookResult HandleCheckoutCompleted(BillingWebhookEvent webhookEvent)
    {
        if (string.Equals(webhookEvent.CheckoutMode, "payment", StringComparison.OrdinalIgnoreCase))
        {
            // Top-up purchase: credits were stamped into the session metadata server-side.
            if (webhookEvent.UserId is null
                || webhookEvent.TopUpCredits is not { } credits
                || credits is < 1 or > 10_000
                || _users.GetUserById(webhookEvent.UserId) is null)
            {
                return BillingWebhookResult.Ignored;
            }

            _credits.GrantTopUp(webhookEvent.UserId, credits);
            return BillingWebhookResult.Processed;
        }

        if (!string.Equals(webhookEvent.CheckoutMode, "subscription", StringComparison.OrdinalIgnoreCase)
            || webhookEvent.UserId is null
            || webhookEvent.CustomerId is null
            || webhookEvent.SubscriptionId is null
            || _users.GetUserById(webhookEvent.UserId) is null)
        {
            return BillingWebhookResult.Ignored;
        }

        // The paying user must not wait for the follow-up subscription event: bind the
        // subscription optimistically as active and promote right away.
        var status = NormalizeStatus(webhookEvent.Status) ?? "active";
        _subscriptions.UpsertSubscription(new BillingSubscription(
            webhookEvent.UserId,
            _provider.Name,
            webhookEvent.CustomerId,
            webhookEvent.SubscriptionId,
            status,
            webhookEvent.CurrentPeriodEnd,
            _clock.UtcNow));
        ApplyRoleTransition(webhookEvent.UserId, status);
        return BillingWebhookResult.Processed;
    }

    private BillingWebhookResult HandleSubscriptionChanged(BillingWebhookEvent webhookEvent, bool deleted)
    {
        var existing = webhookEvent.SubscriptionId is null
            ? null
            : _subscriptions.GetSubscriptionByExternalSubscriptionId(webhookEvent.SubscriptionId);
        existing ??= webhookEvent.UserId is null ? null : _subscriptions.GetSubscriptionByUser(webhookEvent.UserId);

        var userId = webhookEvent.UserId ?? existing?.UserId;
        var subscriptionId = webhookEvent.SubscriptionId ?? existing?.ExternalSubscriptionId;
        var status = deleted ? "canceled" : NormalizeStatus(webhookEvent.Status);
        if (userId is null || subscriptionId is null || status is null || _users.GetUserById(userId) is null)
        {
            // Unresolvable events are ignored, not errors: a later event with better
            // data (or the checkout-completed binding) converges the state.
            return BillingWebhookResult.Ignored;
        }

        _subscriptions.UpsertSubscription(new BillingSubscription(
            userId,
            existing?.Provider ?? _provider.Name,
            webhookEvent.CustomerId ?? existing?.ExternalCustomerId ?? "",
            subscriptionId,
            status,
            webhookEvent.CurrentPeriodEnd ?? existing?.CurrentPeriodEnd,
            _clock.UtcNow));
        ApplyRoleTransition(userId, status);
        return BillingWebhookResult.Processed;
    }

    // Stored roles flip only between Free and Premium; pinned accounts and stored
    // admins are never rewritten by webhooks (pinning already wins at read time).
    private void ApplyRoleTransition(string userId, string subscriptionStatus)
    {
        var user = _users.GetUserById(userId);
        if (user is null || user.Role == UserRole.Admin || _rolePinning.IsPinned(user.NormalizedEmail))
        {
            return;
        }

        var target = BillingRules.GrantsPremium(subscriptionStatus) ? UserRole.Premium : UserRole.Free;
        if (user.Role == target)
        {
            return;
        }

        _users.UpdateUser(user with { Role = target, UpdatedAt = _clock.UtcNow });
    }

    private IEnumerable<BillingTopUpPack> OfferedPacks()
    {
        return _options.TopUpPacks.Where(pack => pack.PriceId.Length > 0);
    }

    private void RequireEnabled()
    {
        if (!_provider.Enabled)
        {
            throw new ValidationException("Billing is not configured.");
        }
    }

    private UserAccount RequireUser(string userId)
    {
        return _users.GetUserById(userId)
            ?? throw new ValidationException("Account was not found.");
    }

    private static string? NormalizeStatus(string? status)
    {
        var normalized = status?.Trim().ToLowerInvariant();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }
}
