using OutfitPlanner.Application.Abstractions;
using OutfitPlanner.Domain;

namespace OutfitPlanner.Application.Services;

public sealed record CreditBalanceInfo(bool Unlimited, int Balance, int MonthlyAllowance, int TrialCredits);

// AI-credit metering over the append-only ledger. Admin accounts bypass the ledger
// entirely (unlimited). Grants are lazy and idempotent: the one-time trial grant on the
// first balance read, and a monthly grant once per UTC calendar month for tiers with a
// monthly allowance. Unspent credits roll over (grants are stored without expiry; the
// expires_at column is reserved for a future tightening).
public sealed class CreditLedgerService
{
    private readonly ICreditLedgerRepository _ledger;
    private readonly PlanCatalog _plans;
    private readonly RolePinningPolicy _rolePinning;
    private readonly IClock _clock;

    public CreditLedgerService(
        ICreditLedgerRepository ledger,
        PlanCatalog plans,
        RolePinningPolicy rolePinning,
        IClock clock)
    {
        _ledger = ledger;
        _plans = plans;
        _rolePinning = rolePinning;
        _clock = clock;
    }

    public CreditBalanceInfo GetBalance(UserAccount user)
    {
        var role = _rolePinning.EffectiveRole(user);
        var limits = _plans.For(role);
        if (role == UserRole.Admin)
        {
            return new CreditBalanceInfo(Unlimited: true, Balance: 0, limits.MonthlyCredits, limits.TrialCredits);
        }

        var now = _clock.UtcNow;
        EnsureGrants(user.Id, limits, now);
        return new CreditBalanceInfo(Unlimited: false, _ledger.GetCreditBalance(user.Id, now), limits.MonthlyCredits, limits.TrialCredits);
    }

    // Throws when the (non-admin) account cannot cover the debit. Zero-credit runs and
    // cache hits never reach this method.
    public void DebitForJob(UserAccount user, Guid tryOnJobId, int credits)
    {
        if (credits <= 0 || _rolePinning.EffectiveRole(user) == UserRole.Admin)
        {
            return;
        }

        var balance = GetBalance(user);
        if (balance.Balance < credits)
        {
            throw new ValidationException($"Not enough AI credits: balance {balance.Balance}, required {credits}. Upgrade your plan or wait for the next monthly allowance.");
        }

        _ledger.AddCreditEntry(new CreditLedgerEntry(
            Guid.NewGuid(),
            user.Id,
            -credits,
            CreditLedgerReason.TryOnSpend,
            tryOnJobId,
            null,
            _clock.UtcNow));
    }

    // Refunds the job's spend once; safe to call for jobs that never debited (admin,
    // free preview, cache hits) or that were already refunded.
    public void RefundJob(string userId, Guid tryOnJobId)
    {
        var entries = _ledger.ListCreditEntriesByJob(tryOnJobId);
        var spent = entries
            .Where(entry => entry.Reason == CreditLedgerReason.TryOnSpend && entry.UserId == userId)
            .Sum(entry => entry.Delta);
        if (spent >= 0 || entries.Any(entry => entry.Reason == CreditLedgerReason.Refund))
        {
            return;
        }

        _ledger.AddCreditEntry(new CreditLedgerEntry(
            Guid.NewGuid(),
            userId,
            -spent,
            CreditLedgerReason.Refund,
            tryOnJobId,
            null,
            _clock.UtcNow));
    }

    // Top-up purchases append non-expiring credits; idempotency is the caller's concern
    // (the billing webhook event gate).
    public void GrantTopUp(string userId, int credits)
    {
        if (credits is < 1 or > 10_000)
        {
            throw new ValidationException("Top-up credits must be between 1 and 10000.");
        }

        _ledger.AddCreditEntry(new CreditLedgerEntry(
            Guid.NewGuid(),
            userId,
            credits,
            CreditLedgerReason.TopUp,
            null,
            null,
            _clock.UtcNow));
    }

    public int AdminAdjust(UserAccount target, int delta)
    {
        if (delta == 0)
        {
            throw new ValidationException("Credit adjustment must not be zero.");
        }

        if (Math.Abs(delta) > 10_000)
        {
            throw new ValidationException("Credit adjustment is limited to 10000 per operation.");
        }

        _ledger.AddCreditEntry(new CreditLedgerEntry(
            Guid.NewGuid(),
            target.Id,
            delta,
            CreditLedgerReason.AdminAdjustment,
            null,
            null,
            _clock.UtcNow));
        return GetBalance(target).Balance;
    }

    private void EnsureGrants(string userId, PlanLimits limits, DateTimeOffset now)
    {
        // Top-up-to-config: accounts granted under an older (smaller) trial receive the
        // difference on their next balance read; lowering the config never claws back.
        var trialGranted = _ledger.GetCreditSumByReason(userId, CreditLedgerReason.TrialGrant);
        if (limits.TrialCredits > trialGranted)
        {
            _ledger.AddCreditEntry(new CreditLedgerEntry(
                Guid.NewGuid(),
                userId,
                limits.TrialCredits - trialGranted,
                CreditLedgerReason.TrialGrant,
                null,
                null,
                now));
        }

        if (limits.MonthlyCredits > 0
            && !_ledger.HasCreditEntryWithReasonSince(userId, CreditLedgerReason.SubscriptionGrant, StartOfUtcMonth(now)))
        {
            _ledger.AddCreditEntry(new CreditLedgerEntry(
                Guid.NewGuid(),
                userId,
                limits.MonthlyCredits,
                CreditLedgerReason.SubscriptionGrant,
                null,
                null,
                now));
        }
    }

    private static DateTimeOffset StartOfUtcMonth(DateTimeOffset now)
    {
        var utc = now.ToUniversalTime();
        return new DateTimeOffset(utc.Year, utc.Month, 1, 0, 0, 0, TimeSpan.Zero);
    }
}
