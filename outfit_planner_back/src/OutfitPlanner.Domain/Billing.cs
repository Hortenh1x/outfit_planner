namespace OutfitPlanner.Domain;

// Provider-owned subscription state mirrored locally. Status is the normalized
// (lowercase) provider status; BillingRules decides which statuses grant Premium.
public sealed record BillingSubscription(
    string UserId,
    string Provider,
    string ExternalCustomerId,
    string ExternalSubscriptionId,
    string Status,
    DateTimeOffset? CurrentPeriodEnd,
    DateTimeOffset UpdatedAt);

// Webhook idempotency marker: an event id is processed at most once.
public sealed record ProcessedBillingEvent(string EventId, DateTimeOffset ProcessedAt);

public static class BillingRules
{
    // past_due keeps Premium as a grace window while the provider retries payment.
    private static readonly string[] PremiumStatuses = { "active", "trialing", "past_due" };

    public static bool GrantsPremium(string? status)
    {
        return status is not null
            && PremiumStatuses.Contains(status.Trim().ToLowerInvariant());
    }
}
