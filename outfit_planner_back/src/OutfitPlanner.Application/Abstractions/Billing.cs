using OutfitPlanner.Domain;

namespace OutfitPlanner.Application.Abstractions;

public interface ISubscriptionRepository
{
    void UpsertSubscription(BillingSubscription subscription);
    BillingSubscription? GetSubscriptionByUser(string userId);
    BillingSubscription? GetSubscriptionByExternalSubscriptionId(string externalSubscriptionId);
}

public interface IBillingEventRepository
{
    // Atomic first-time-wins insert; false means the event was already processed.
    bool TryRecordBillingEvent(string eventId, DateTimeOffset processedAt);
}

public enum BillingWebhookEventKind
{
    Ignored,
    CheckoutCompleted,
    SubscriptionUpdated,
    SubscriptionDeleted
}

// Provider-agnostic webhook payload; providers map their raw events into this shape.
public sealed record BillingWebhookEvent(
    string EventId,
    BillingWebhookEventKind Kind,
    string? UserId = null,
    string? CustomerId = null,
    string? SubscriptionId = null,
    string? Status = null,
    DateTimeOffset? CurrentPeriodEnd = null,
    string? CheckoutMode = null,
    string? TopUpPackId = null,
    int? TopUpCredits = null);

public sealed record BillingTopUpPack(string Id, int Credits, string PriceId, string? DisplayPrice);

// Built by the Api layer from configuration; price ids are opaque provider strings.
public sealed record BillingOptions(
    string PremiumPriceId,
    string? PremiumDisplayPrice,
    IReadOnlyList<BillingTopUpPack> TopUpPacks,
    string CheckoutSuccessUrl,
    string CheckoutCancelUrl,
    string PortalReturnUrl)
{
    public static BillingOptions Empty { get; } = new("", null, Array.Empty<BillingTopUpPack>(), "", "", "");
}

public interface IBillingProvider
{
    string Name { get; }
    bool Enabled { get; }
    Task<string> CreateSubscriptionCheckoutAsync(UserAccount user, string priceId, string successUrl, string cancelUrl, CancellationToken cancellationToken);
    Task<string> CreateTopUpCheckoutAsync(UserAccount user, BillingTopUpPack pack, string successUrl, string cancelUrl, CancellationToken cancellationToken);
    Task<string> CreatePortalSessionAsync(string customerId, string returnUrl, CancellationToken cancellationToken);
    // Verifies the signature; null means the payload/signature pair is invalid (→ 400).
    BillingWebhookEvent? ParseWebhookEvent(string payload, string? signatureHeader);
}
