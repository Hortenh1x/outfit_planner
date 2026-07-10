using OutfitPlanner.Application.Abstractions;
using OutfitPlanner.Domain;
using Stripe;
using Stripe.Checkout;

namespace OutfitPlanner.Infrastructure.Billing;

public sealed record StripeBillingSettings(string SecretKey, string WebhookSecret);

// Stripe-backed billing: Checkout Sessions for the subscription and top-up purchases,
// the customer portal for self-service management, and signature-verified webhook
// parsing into the provider-agnostic BillingWebhookEvent shape. No card data ever
// touches the API; Stripe hosts every payment surface.
public sealed class StripeBillingProvider : IBillingProvider
{
    private const long SignatureToleranceSeconds = 300;

    private readonly StripeBillingSettings _settings;
    private readonly StripeClient _client;

    public StripeBillingProvider(StripeBillingSettings settings)
    {
        _settings = settings;
        _client = new StripeClient(settings.SecretKey);
    }

    public string Name => "stripe";

    public bool Enabled => true;

    public async Task<string> CreateSubscriptionCheckoutAsync(UserAccount user, string priceId, string successUrl, string cancelUrl, CancellationToken cancellationToken)
    {
        var service = new SessionService(_client);
        var session = await service.CreateAsync(new SessionCreateOptions
        {
            Mode = "subscription",
            ClientReferenceId = user.Id,
            CustomerEmail = user.Email,
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            LineItems = new List<SessionLineItemOptions>
            {
                new() { Price = priceId, Quantity = 1 }
            },
            // The subscription carries the user id so lifecycle webhooks resolve the
            // account without a session lookup.
            SubscriptionData = new SessionSubscriptionDataOptions
            {
                Metadata = new Dictionary<string, string> { ["userId"] = user.Id }
            },
            Metadata = new Dictionary<string, string>
            {
                ["userId"] = user.Id,
                ["type"] = "subscription"
            }
        }, cancellationToken: cancellationToken);
        return session.Url;
    }

    public async Task<string> CreateTopUpCheckoutAsync(UserAccount user, BillingTopUpPack pack, string successUrl, string cancelUrl, CancellationToken cancellationToken)
    {
        var service = new SessionService(_client);
        var session = await service.CreateAsync(new SessionCreateOptions
        {
            Mode = "payment",
            ClientReferenceId = user.Id,
            CustomerEmail = user.Email,
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            LineItems = new List<SessionLineItemOptions>
            {
                new() { Price = pack.PriceId, Quantity = 1 }
            },
            // Credits are stamped server-side at session creation; the webhook trusts
            // this metadata (sanity-bounded in the application layer).
            Metadata = new Dictionary<string, string>
            {
                ["userId"] = user.Id,
                ["type"] = "top-up",
                ["packId"] = pack.Id,
                ["credits"] = pack.Credits.ToString()
            }
        }, cancellationToken: cancellationToken);
        return session.Url;
    }

    public async Task<string> CreatePortalSessionAsync(string customerId, string returnUrl, CancellationToken cancellationToken)
    {
        var service = new Stripe.BillingPortal.SessionService(_client);
        var session = await service.CreateAsync(new Stripe.BillingPortal.SessionCreateOptions
        {
            Customer = customerId,
            ReturnUrl = returnUrl
        }, cancellationToken: cancellationToken);
        return session.Url;
    }

    public BillingWebhookEvent? ParseWebhookEvent(string payload, string? signatureHeader)
    {
        // Fail closed: an empty webhook secret makes signature verification meaningless —
        // EventUtility.ConstructEvent would HMAC with an empty key and accept a forged
        // event. Reject every webhook until a real secret is configured, so a partial
        // "SecretKey set, WebhookSecret blank" config cannot be exploited to forge
        // credit grants or role changes.
        if (string.IsNullOrWhiteSpace(_settings.WebhookSecret)
            || string.IsNullOrWhiteSpace(payload)
            || string.IsNullOrWhiteSpace(signatureHeader))
        {
            return null;
        }

        Event stripeEvent;
        try
        {
            // Tolerant of API version drift: we only read stable fields below.
            stripeEvent = EventUtility.ConstructEvent(
                payload,
                signatureHeader,
                _settings.WebhookSecret,
                SignatureToleranceSeconds,
                throwOnApiVersionMismatch: false);
        }
        catch (StripeException)
        {
            return null;
        }

        return stripeEvent.Type switch
        {
            "checkout.session.completed" when stripeEvent.Data.Object is Session session => new BillingWebhookEvent(
                stripeEvent.Id,
                BillingWebhookEventKind.CheckoutCompleted,
                UserId: session.ClientReferenceId ?? MetadataValue(session.Metadata, "userId"),
                CustomerId: session.CustomerId,
                SubscriptionId: session.SubscriptionId,
                CheckoutMode: session.Mode,
                TopUpPackId: MetadataValue(session.Metadata, "packId"),
                TopUpCredits: int.TryParse(MetadataValue(session.Metadata, "credits"), out var credits) ? credits : null),
            // A fresh subscription may only emit `created`, so both map to Updated.
            "customer.subscription.created" or "customer.subscription.updated"
                when stripeEvent.Data.Object is Subscription updated => SubscriptionEvent(stripeEvent.Id, BillingWebhookEventKind.SubscriptionUpdated, updated),
            "customer.subscription.deleted" when stripeEvent.Data.Object is Subscription deleted =>
                SubscriptionEvent(stripeEvent.Id, BillingWebhookEventKind.SubscriptionDeleted, deleted),
            _ => new BillingWebhookEvent(stripeEvent.Id, BillingWebhookEventKind.Ignored)
        };
    }

    private static BillingWebhookEvent SubscriptionEvent(string eventId, BillingWebhookEventKind kind, Subscription subscription)
    {
        return new BillingWebhookEvent(
            eventId,
            kind,
            UserId: MetadataValue(subscription.Metadata, "userId"),
            CustomerId: subscription.CustomerId,
            SubscriptionId: subscription.Id,
            Status: subscription.Status,
            CurrentPeriodEnd: PeriodEndOf(subscription));
    }

    // Stripe API 2025+ moved the current period onto subscription items; the
    // subscription-level period is the latest item period.
    private static DateTimeOffset? PeriodEndOf(Subscription subscription)
    {
        var ends = subscription.Items?.Data?
            .Select(item => item.CurrentPeriodEnd)
            .Where(value => value != default)
            .ToArray();
        if (ends is null || ends.Length == 0)
        {
            return null;
        }

        return new DateTimeOffset(DateTime.SpecifyKind(ends.Max(), DateTimeKind.Utc));
    }

    private static string? MetadataValue(IDictionary<string, string>? metadata, string key)
    {
        return metadata is not null && metadata.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
    }
}
