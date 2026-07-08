namespace OutfitPlanner.Domain;

public enum CreditLedgerReason
{
    TrialGrant,
    SubscriptionGrant,
    TopUp,
    TryOnSpend,
    Refund,
    AdminAdjustment
}

// Append-only AI-credit ledger row. Positive deltas grant, negative deltas spend; the
// balance is the sum of non-expired rows. ExpiresAt is reserved for future expiring grants
// (current grants roll over and never expire).
public sealed record CreditLedgerEntry(
    Guid Id,
    string UserId,
    int Delta,
    CreditLedgerReason Reason,
    Guid? TryOnJobId,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset CreatedAt);
