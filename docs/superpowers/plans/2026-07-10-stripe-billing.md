# Trial Credits 8 + Stripe Billing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use subagent-driven-development (recommended) or executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Raise the Free trial grant to 8 credits with top-up-to-config semantics, and implement Stripe billing (checkout, top-ups, portal, webhooks, role transitions, admin visibility, upgrade UI) to "insert the API key" readiness.

**Architecture:** Onion-preserving: Domain gets `BillingSubscription` + `BillingRules`; Application gets repositories/provider abstractions + `BillingService`; Infrastructure gets Stripe.net provider + storage in all three stores; Api wires config-selected providers and endpoints (webhook is anonymous + signature-verified); frontend adds an `/upgrade` page and billing blocks. Spec: `docs/superpowers/specs/2026-07-10-stripe-billing-design.md`.

**Tech Stack:** .NET 10 Minimal API, Npgsql + DbUp migrations, Stripe.net, React+TS+Vite, TanStack Query, custom console test runner (`dotnet run --project outfit_planner_back/tests/...`).

**Conventions for every task:** backend verification command is `dotnet run --project outfit_planner_back/tests/OutfitPlanner.Api.Tests/OutfitPlanner.Api.Tests.csproj`; never run backend and frontend `npm` verification in parallel (OpenAPI race); commit only files this plan touches (repo has unrelated untracked files — never `git add -A`); keep committed `appsettings.json` values empty for anything secret.

---

### Task 1: Trial credits 8 + top-up-to-config grant

**Files:**
- Modify: `outfit_planner_back/src/OutfitPlanner.Application/Abstractions/Entitlements.cs:52` (6 → 8)
- Modify: `outfit_planner_back/src/OutfitPlanner.Application/Abstractions/Repositories.cs` (`ICreditLedgerRepository`)
- Modify: `outfit_planner_back/src/OutfitPlanner.Application/Services/CreditLedgerService.cs` (`EnsureGrants`)
- Modify: `outfit_planner_back/src/OutfitPlanner.Infrastructure/Storage/InMemoryOutfitStore.cs`, `FileBackedOutfitStore.cs`, `PostgresOutfitStore.cs`
- Test: `outfit_planner_back/tests/OutfitPlanner.Api.Tests/Program.cs`

- [ ] **Step 1: Update `TestCreditLedgerGrantsDebitsAndRefunds` to the new numbers and add the top-up test (failing first).**

In the existing test replace the 6-based assertions: initial balance `8` (twice), after `DebitForJob(freeUser, jobId, 2)` → `6`, after double refund → `8`, `AdminAdjust(freeUser, 4)` → `12`. Register a new test in the list:

```csharp
("trial grants top up existing accounts to the configured amount", TestTrialGrantTopsUpToConfiguredAmount),
```

```csharp
static void TestTrialGrantTopsUpToConfiguredAmount()
{
    var store = new InMemoryOutfitStore();
    var pinning = TestRolePinning();
    var clock = new SystemClock();
    var auth = new AuthService(store, new TestPasswordHasher(), new TestAuthTokenService(), clock, pinning);
    var user = store.GetUserById(auth.RegisterWithPassword("topup-trial@example.com", "abc12345", "abc12345").User.Id)
        ?? throw new InvalidOperationException("Account was not stored.");

    // Simulate an account granted under the old 6-credit trial.
    store.AddCreditEntry(new CreditLedgerEntry(Guid.NewGuid(), user.Id, 6, CreditLedgerReason.TrialGrant, null, null, clock.UtcNow));

    var credits = new CreditLedgerService(store, PlanCatalog.Default, pinning, clock);
    AssertEqual(8, credits.GetBalance(user).Balance, "existing trial accounts should be topped up to the configured amount.");
    AssertEqual(8, credits.GetBalance(user).Balance, "the top-up must not repeat.");
    AssertEqual(2, store.ListCreditEntriesByUser(user.Id).Count, "the top-up should append exactly one extra grant entry.");

    // Lowering the config must never claw back.
    var lowered = new PlanCatalog(
        PlanCatalog.Default.For(UserRole.Free) with { TrialCredits = 4 },
        PlanCatalog.Default.For(UserRole.Premium),
        PlanCatalog.Default.For(UserRole.Admin));
    var loweredCredits = new CreditLedgerService(store, lowered, pinning, clock);
    AssertEqual(8, loweredCredits.GetBalance(user).Balance, "a lowered trial config must not claw back granted credits.");
}
```

- [ ] **Step 2: Run backend tests, confirm the new test fails** (`GetCreditSumByReason` missing → compile error is the failure signal here).

- [ ] **Step 3: Implement.**

`Entitlements.cs`: `TrialCredits: 6` → `TrialCredits: 8` (Free only). `Repositories.cs` add to `ICreditLedgerRepository`:

```csharp
int GetCreditSumByReason(string userId, CreditLedgerReason reason);
```

`CreditLedgerService.EnsureGrants` trial branch becomes:

```csharp
var trialGranted = _ledger.GetCreditSumByReason(userId, CreditLedgerReason.TrialGrant);
if (limits.TrialCredits > trialGranted)
{
    _ledger.AddCreditEntry(new CreditLedgerEntry(
        Guid.NewGuid(), userId, limits.TrialCredits - trialGranted,
        CreditLedgerReason.TrialGrant, null, null, now));
}
```

InMemory implementation (next to the other ledger methods):

```csharp
public int GetCreditSumByReason(string userId, CreditLedgerReason reason)
{
    lock (_lock)
    {
        return _creditLedger.Values
            .Where(entry => entry.UserId == userId && entry.Reason == reason)
            .Sum(entry => entry.Delta);
    }
}
```

FileBacked: delegate `=> _inner.GetCreditSumByReason(userId, reason);` (read-only, no snapshot save). Postgres:

```csharp
public int GetCreditSumByReason(string userId, CreditLedgerReason reason)
{
    using var command = _dataSource.CreateCommand("""
        select coalesce(sum(delta), 0)
        from account_credit_ledger
        where user_id = @user_id and reason = @reason
        """);
    command.Parameters.AddWithValue("user_id", userId);
    command.Parameters.AddWithValue("reason", reason.ToString());
    return Convert.ToInt32(command.ExecuteScalar());
}
```

- [ ] **Step 4: Run backend tests — all pass.**
- [ ] **Step 5: Commit** `feat: raise the Free trial grant to 8 credits with top-up-to-config semantics`.

### Task 2: Billing domain + Application abstractions

**Files:**
- Create: `outfit_planner_back/src/OutfitPlanner.Domain/Billing.cs`
- Create: `outfit_planner_back/src/OutfitPlanner.Application/Abstractions/Billing.cs`
- Test: `outfit_planner_back/tests/OutfitPlanner.Api.Tests/Program.cs`

- [ ] **Step 1: Failing test** (register `("billing rules map subscription statuses to premium", TestBillingRulesMapStatuses)`):

```csharp
static void TestBillingRulesMapStatuses()
{
    foreach (var premium in new[] { "active", "trialing", "past_due", " Active " })
    {
        AssertTrue(BillingRules.GrantsPremium(premium), $"status '{premium}' should grant premium.");
    }
    foreach (var free in new[] { "canceled", "unpaid", "incomplete", "incomplete_expired", "paused", "", null })
    {
        AssertTrue(!BillingRules.GrantsPremium(free), $"status '{free}' must not grant premium.");
    }
}
```

- [ ] **Step 2: Implement Domain** `Billing.cs`:

```csharp
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
```

- [ ] **Step 3: Implement Application** `Abstractions/Billing.cs`:

```csharp
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
```

- [ ] **Step 4: Run backend tests — pass.**
- [ ] **Step 5: Commit** `feat: billing domain model and application abstractions`.

### Task 3: `CreditLedgerService.GrantTopUp` + `BillingService`

**Files:**
- Modify: `outfit_planner_back/src/OutfitPlanner.Application/Services/CreditLedgerService.cs`
- Create: `outfit_planner_back/src/OutfitPlanner.Application/Services/BillingService.cs`
- Test: `outfit_planner_back/tests/OutfitPlanner.Api.Tests/Program.cs`

- [ ] **Step 1: Failing tests.** Register:

```csharp
("billing service gates checkout portal and top-ups by plan", TestBillingServiceGatesCheckoutAndPortal),
("billing webhooks upsert subscriptions transition roles and grant top-ups idempotently", TestBillingWebhooksDriveRolesAndTopUps),
```

Fake provider (near the other test fakes):

```csharp
sealed class FakeBillingProvider : IBillingProvider
{
    public string Name => "fake";
    public bool Enabled { get; set; } = true;
    public BillingWebhookEvent? NextEvent { get; set; }
    public Task<string> CreateSubscriptionCheckoutAsync(UserAccount user, string priceId, string successUrl, string cancelUrl, CancellationToken cancellationToken)
        => Task.FromResult("https://billing.example/checkout");
    public Task<string> CreateTopUpCheckoutAsync(UserAccount user, BillingTopUpPack pack, string successUrl, string cancelUrl, CancellationToken cancellationToken)
        => Task.FromResult($"https://billing.example/topup/{pack.Id}");
    public Task<string> CreatePortalSessionAsync(string customerId, string returnUrl, CancellationToken cancellationToken)
        => Task.FromResult($"https://billing.example/portal/{customerId}");
    public BillingWebhookEvent? ParseWebhookEvent(string payload, string? signatureHeader)
        => signatureHeader == "valid" ? NextEvent : null;
}
```

Test bodies (helper builds the service; `TestBillingOptions()` returns options with one pack):

```csharp
static BillingOptions TestBillingOptions() => new(
    PremiumPriceId: "price_premium",
    PremiumDisplayPrice: "$9/mo",
    TopUpPacks: new[] { new BillingTopUpPack("pack-20", 20, "price_pack20", "$5") },
    CheckoutSuccessUrl: "https://app.example/upgrade?checkout=success",
    CheckoutCancelUrl: "https://app.example/upgrade?checkout=cancelled",
    PortalReturnUrl: "https://app.example/upgrade");

static void TestBillingServiceGatesCheckoutAndPortal()
{
    var store = new InMemoryOutfitStore();
    var pinning = TestRolePinning();
    var clock = new SystemClock();
    var credits = new CreditLedgerService(store, PlanCatalog.Default, pinning, clock);
    var auth = new AuthService(store, new TestPasswordHasher(), new TestAuthTokenService(), clock, pinning);
    var provider = new FakeBillingProvider();
    var billing = new BillingService(store, store, store, credits, provider, TestBillingOptions(), pinning, clock);

    var free = store.GetUserById(auth.RegisterWithPassword("billing-free@example.com", "abc12345", "abc12345").User.Id)!;
    var premium = store.GetUserById(auth.RegisterWithPassword("olya.shaydur@gmail.com", "abc12345", "abc12345").User.Id)!;
    var admin = store.GetUserById(auth.RegisterWithPassword("dmytro.bolibok@gmail.com", "abc12345", "abc12345").User.Id)!;

    AssertEqual("https://billing.example/checkout", billing.StartSubscriptionCheckoutAsync(free.Id, CancellationToken.None).GetAwaiter().GetResult(),
        "free accounts should get a subscription checkout url.");
    AssertThrows<InvalidOperationException>(() => billing.StartSubscriptionCheckoutAsync(premium.Id, CancellationToken.None).GetAwaiter().GetResult(),
        "premium accounts must not start a second subscription checkout");
    AssertThrows<InvalidOperationException>(() => billing.StartSubscriptionCheckoutAsync(admin.Id, CancellationToken.None).GetAwaiter().GetResult(),
        "admin accounts are not sellable");

    AssertThrows<InvalidOperationException>(() => billing.StartTopUpCheckoutAsync(free.Id, "pack-20", CancellationToken.None).GetAwaiter().GetResult(),
        "top-ups are a premium feature");
    AssertEqual("https://billing.example/topup/pack-20", billing.StartTopUpCheckoutAsync(premium.Id, "pack-20", CancellationToken.None).GetAwaiter().GetResult(),
        "premium accounts should get a top-up checkout url.");
    AssertThrows<InvalidOperationException>(() => billing.StartTopUpCheckoutAsync(premium.Id, "missing", CancellationToken.None).GetAwaiter().GetResult(),
        "unknown packs must be rejected");

    AssertThrows<InvalidOperationException>(() => billing.CreatePortalAsync(free.Id, CancellationToken.None).GetAwaiter().GetResult(),
        "portal requires an existing subscription");

    var status = billing.GetStatus(free.Id);
    AssertTrue(status.Enabled && status.SubscriptionPriceConfigured, "billing status should reflect the configured provider.");
    AssertEqual(1, status.TopUpPacks.Count, "configured packs should be offered.");
    AssertTrue(status.Subscription is null && !status.PortalAvailable, "accounts without subscriptions have no portal.");

    provider.Enabled = false;
    var disabled = new BillingService(store, store, store, credits, provider, BillingOptions.Empty, pinning, clock);
    AssertTrue(!disabled.GetStatus(free.Id).Enabled, "disabled providers must read as disabled.");
    AssertThrows<InvalidOperationException>(() => disabled.StartSubscriptionCheckoutAsync(free.Id, CancellationToken.None).GetAwaiter().GetResult(),
        "checkout must be rejected while billing is disabled");
}

static void TestBillingWebhooksDriveRolesAndTopUps()
{
    var store = new InMemoryOutfitStore();
    var pinning = TestRolePinning();
    var clock = new SystemClock();
    var credits = new CreditLedgerService(store, PlanCatalog.Default, pinning, clock);
    var auth = new AuthService(store, new TestPasswordHasher(), new TestAuthTokenService(), clock, pinning);
    var provider = new FakeBillingProvider();
    var billing = new BillingService(store, store, store, credits, provider, TestBillingOptions(), pinning, clock);

    var user = store.GetUserById(auth.RegisterWithPassword("webhook-user@example.com", "abc12345", "abc12345").User.Id)!;

    provider.NextEvent = new BillingWebhookEvent("evt-1", BillingWebhookEventKind.CheckoutCompleted,
        UserId: user.Id, CustomerId: "cus_1", SubscriptionId: "sub_1", CheckoutMode: "subscription");
    AssertEqual("processed", billing.HandleWebhookAsync("{}", "valid", CancellationToken.None).GetAwaiter().GetResult().Status,
        "subscription checkout completion should process.");
    AssertEqual(UserRole.Premium, store.GetUserById(user.Id)!.Role, "checkout completion should promote the stored role.");
    AssertEqual("sub_1", store.GetSubscriptionByUser(user.Id)!.ExternalSubscriptionId, "the subscription row should be bound to the user.");

    AssertEqual("duplicate", billing.HandleWebhookAsync("{}", "valid", CancellationToken.None).GetAwaiter().GetResult().Status,
        "replayed event ids must be no-ops.");

    provider.NextEvent = new BillingWebhookEvent("evt-2", BillingWebhookEventKind.SubscriptionUpdated,
        SubscriptionId: "sub_1", Status: "past_due", CurrentPeriodEnd: clock.UtcNow.AddDays(30));
    billing.HandleWebhookAsync("{}", "valid", CancellationToken.None).GetAwaiter().GetResult();
    AssertEqual(UserRole.Premium, store.GetUserById(user.Id)!.Role, "past_due keeps premium as a grace window.");
    AssertEqual("past_due", store.GetSubscriptionByUser(user.Id)!.Status, "subscription status should update by external id lookup.");

    provider.NextEvent = new BillingWebhookEvent("evt-3", BillingWebhookEventKind.SubscriptionDeleted, SubscriptionId: "sub_1");
    billing.HandleWebhookAsync("{}", "valid", CancellationToken.None).GetAwaiter().GetResult();
    AssertEqual(UserRole.Free, store.GetUserById(user.Id)!.Role, "deleted subscriptions should demote the stored role.");
    AssertEqual("canceled", store.GetSubscriptionByUser(user.Id)!.Status, "deleted subscriptions should read canceled.");

    var balanceBefore = credits.GetBalance(store.GetUserById(user.Id)!).Balance;
    provider.NextEvent = new BillingWebhookEvent("evt-4", BillingWebhookEventKind.CheckoutCompleted,
        UserId: user.Id, CheckoutMode: "payment", TopUpPackId: "pack-20", TopUpCredits: 20);
    billing.HandleWebhookAsync("{}", "valid", CancellationToken.None).GetAwaiter().GetResult();
    AssertEqual(balanceBefore + 20, credits.GetBalance(store.GetUserById(user.Id)!).Balance, "top-up checkouts should grant credits.");

    // Pinned accounts are exempt from webhook-driven role changes.
    var pinnedPremium = store.GetUserById(auth.RegisterWithPassword("olya.shaydur@gmail.com", "abc12345", "abc12345").User.Id)!;
    provider.NextEvent = new BillingWebhookEvent("evt-5", BillingWebhookEventKind.CheckoutCompleted,
        UserId: pinnedPremium.Id, CustomerId: "cus_2", SubscriptionId: "sub_2", CheckoutMode: "subscription");
    billing.HandleWebhookAsync("{}", "valid", CancellationToken.None).GetAwaiter().GetResult();
    provider.NextEvent = new BillingWebhookEvent("evt-6", BillingWebhookEventKind.SubscriptionDeleted, SubscriptionId: "sub_2");
    billing.HandleWebhookAsync("{}", "valid", CancellationToken.None).GetAwaiter().GetResult();
    AssertEqual(UserRole.Premium, store.GetUserById(pinnedPremium.Id)!.Role, "pinned stored roles must not be rewritten by webhooks.");

    AssertThrows<InvalidOperationException>(() => billing.HandleWebhookAsync("{}", "bogus", CancellationToken.None).GetAwaiter().GetResult(),
        "invalid signatures must be rejected");

    provider.NextEvent = new BillingWebhookEvent("evt-7", BillingWebhookEventKind.SubscriptionUpdated, SubscriptionId: "sub_unknown", Status: "active");
    AssertEqual("ignored", billing.HandleWebhookAsync("{}", "valid", CancellationToken.None).GetAwaiter().GetResult().Status,
        "unresolvable subscriptions are ignored, not errors.");
}
```

- [ ] **Step 2: Implement `GrantTopUp`** in `CreditLedgerService`:

```csharp
// Top-up purchases append non-expiring credits; idempotency is the caller's concern
// (the billing webhook event gate).
public void GrantTopUp(string userId, int credits)
{
    if (credits is < 1 or > 10_000)
    {
        throw new ValidationException("Top-up credits must be between 1 and 10000.");
    }

    _ledger.AddCreditEntry(new CreditLedgerEntry(
        Guid.NewGuid(), userId, credits, CreditLedgerReason.TopUp, null, null, _clock.UtcNow));
}
```

- [ ] **Step 3: Implement `BillingService`** (`Services/BillingService.cs`). Public surface:

```csharp
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
public sealed record BillingWebhookResult(string Status); // processed | duplicate | ignored
```

Constructor `(IUserAccountRepository users, ISubscriptionRepository subscriptions, IBillingEventRepository events, CreditLedgerService credits, IBillingProvider provider, BillingOptions options, RolePinningPolicy rolePinning, IClock clock)`. Behavior (mirror the tests exactly):

- `GetStatus`: `Enabled = provider.Enabled`; offered packs = `options.TopUpPacks` with non-empty `PriceId`; `SubscriptionPriceConfigured = options.PremiumPriceId != ""`; subscription info from `GetSubscriptionByUser` (`PremiumActive = BillingRules.GrantsPremium(status)`); `PortalAvailable = subscription is not null`.
- `StartSubscriptionCheckoutAsync`: require `provider.Enabled` and configured price; effective role via `RolePinningPolicy` must be `Free` (Premium → "You already have Premium. Manage your subscription from the billing portal."; Admin → "Admin accounts do not need a subscription."); delegate to provider with `options` URLs.
- `StartTopUpCheckoutAsync`: require enabled; effective role `Premium` (Free → upgrade first; Admin → unlimited credits); pack lookup by id with non-empty `PriceId` else `ValidationException("Unknown top-up pack.")`.
- `CreatePortalAsync`: require enabled + subscription row → provider portal with `ExternalCustomerId`; else `ValidationException("No subscription to manage.")`.
- `HandleWebhookAsync`: enabled check; `ParseWebhookEvent` null → `ValidationException("Invalid webhook signature.")`; `TryRecordBillingEvent` false → `duplicate`. `Ignored` → `ignored`. `CheckoutCompleted(payment)`: needs `UserId` + `TopUpCredits` (else `ignored`), existing user check, `GrantTopUp`, `processed`. `CheckoutCompleted(subscription)`: needs `UserId`+`CustomerId`+`SubscriptionId` (else `ignored`), upsert `BillingSubscription(userId, provider.Name, customerId, subscriptionId, Normalize(evt.Status) ?? "active", evt.CurrentPeriodEnd, now)`, role transition, `processed`. `SubscriptionUpdated`/`Deleted`: resolve existing row by `evt.SubscriptionId` (fallback: `evt.UserId` → row by user); resolve `userId = evt.UserId ?? existing?.UserId` (null → `ignored`); status = deleted ? "canceled" : `Normalize(evt.Status)` (null → `ignored`); upsert (customer id from event else existing else "");  role transition; `processed`.
- Role transition (private): fetch user; skip when null, stored role `Admin`, or `rolePinning.PinnedRole(email) is not null` (check exact member name in `Abstractions/RolePinning.cs` — it is used as `_rolePinning.PinnedRole(normalizedEmail)` in `AuthService.cs:64`); target = `GrantsPremium(status) ? Premium : Free`; write `users.UpdateUser(user with { Role = target, UpdatedAt = clock.UtcNow })` only when different.
- `Normalize(string? s)` → trimmed lowercase or null when blank.

Note: `InMemoryOutfitStore` will not implement `ISubscriptionRepository` until Task 4 — implement Task 4's store changes together with this task if the test compile requires it, but keep the commits separate only if both build; otherwise squash Tasks 3–4 into one commit.

- [ ] **Step 4: Run backend tests — pass** (Tasks 3+4 combined if needed).
- [ ] **Step 5: Commit** `feat: billing service with webhook-driven roles and credit top-ups`.

### Task 4: Storage — InMemory + FileBacked + Postgres + migration

**Files:**
- Modify: `outfit_planner_back/src/OutfitPlanner.Infrastructure/Storage/InMemoryOutfitStore.cs` (interfaces, dictionaries, snapshot)
- Modify: `outfit_planner_back/src/OutfitPlanner.Infrastructure/Storage/FileBackedOutfitStore.cs`
- Modify: `outfit_planner_back/src/OutfitPlanner.Infrastructure/Storage/PostgresOutfitStore.cs`
- Create: `outfit_planner_back/database/migrations/013_billing.sql`
- Modify: `database/schema.sql` → actual path `outfit_planner_back/database/schema.sql`
- Test: `outfit_planner_back/tests/OutfitPlanner.Api.Tests/Program.cs`

- [ ] **Step 1: Failing tests.** Register:

```csharp
("local file store persists billing records across restarts", TestLocalFileStorePersistsBillingRecords),
("postgres schema contains billing tables", TestPostgresSchemaContainsBillingTables),
```

`TestLocalFileStorePersistsBillingRecords`: mirror `TestLocalFileStorePersistsRecordsAcrossRestarts` (temp dir, first store instance: register user, `UpsertSubscription(new BillingSubscription(userId, "stripe", "cus_1", "sub_1", "active", null, DateTimeOffset.UtcNow))`, `TryRecordBillingEvent("evt-1", ...)` → true; second instance from the same path: subscription round-trips, `TryRecordBillingEvent("evt-1", ...)` → false, `GetSubscriptionByExternalSubscriptionId("sub_1")` found). `TestPostgresSchemaContainsBillingTables`: mirror `TestPostgresSchemaContainsCreditLedger` — assert `outfit_planner_back/database/schema.sql` and `013_billing.sql` contain `billing_subscriptions`, `billing_webhook_events`, `on delete cascade`, `on conflict` is not needed in schema — just table/column names. Also extend the existing `TestPostgresStoreImplementsRepositoryPorts` / `TestLocalFileStoreImplementsRepositoryPorts` interface lists with `ISubscriptionRepository`, `IBillingEventRepository` (read those tests first; they assert `typeof(...).IsAssignableFrom`).

- [ ] **Step 2: InMemory.** Add `ISubscriptionRepository, IBillingEventRepository` to the class interface list; fields:

```csharp
private readonly Dictionary<string, BillingSubscription> _subscriptions = new(StringComparer.Ordinal);
private readonly Dictionary<string, DateTimeOffset> _billingEvents = new(StringComparer.Ordinal);
```

Methods:

```csharp
public void UpsertSubscription(BillingSubscription subscription)
{
    lock (_lock) { _subscriptions[subscription.UserId] = subscription; }
}

public BillingSubscription? GetSubscriptionByUser(string userId)
{
    lock (_lock) { return _subscriptions.TryGetValue(userId, out var subscription) ? subscription : null; }
}

public BillingSubscription? GetSubscriptionByExternalSubscriptionId(string externalSubscriptionId)
{
    lock (_lock)
    {
        return _subscriptions.Values.FirstOrDefault(subscription =>
            string.Equals(subscription.ExternalSubscriptionId, externalSubscriptionId, StringComparison.Ordinal));
    }
}

public bool TryRecordBillingEvent(string eventId, DateTimeOffset processedAt)
{
    lock (_lock) { return _billingEvents.TryAdd(eventId, processedAt); }
}
```

Snapshot record gains (nullable-with-default, like `CreditLedger`):

```csharp
BillingSubscription[]? Subscriptions = null,
ProcessedBillingEvent[]? BillingEvents = null
```

`Empty` gains two `Array.Empty<...>()`; `ExportSnapshot` exports `_subscriptions.Values.ToArray()` and `_billingEvents.Select(pair => new ProcessedBillingEvent(pair.Key, pair.Value)).ToArray()`; `LoadSnapshot` loads both (null → empty). Also purge on user delete: check `DeleteUserById` for how per-user data is removed and drop the user's subscription there (webhook events are global, keep).

- [ ] **Step 3: FileBacked.** Add the two interfaces; delegate reads; writes delegate + `SaveSnapshot()`:

```csharp
public void UpsertSubscription(BillingSubscription subscription)
{
    lock (_ioLock) { _inner.UpsertSubscription(subscription); SaveSnapshot(); }
}
public bool TryRecordBillingEvent(string eventId, DateTimeOffset processedAt)
{
    lock (_ioLock)
    {
        var recorded = _inner.TryRecordBillingEvent(eventId, processedAt);
        if (recorded) { SaveSnapshot(); }
        return recorded;
    }
}
```

(match the file's actual lock/save pattern — read the neighboring methods first).

- [ ] **Step 4: Postgres.** Add interfaces + SQL:

```csharp
public void UpsertSubscription(BillingSubscription subscription)
{
    using var command = _dataSource.CreateCommand("""
        insert into billing_subscriptions (user_id, provider, external_customer_id, external_subscription_id, status, current_period_end, updated_at)
        values (@user_id, @provider, @external_customer_id, @external_subscription_id, @status, @current_period_end, @updated_at)
        on conflict (user_id) do update set
            provider = excluded.provider,
            external_customer_id = excluded.external_customer_id,
            external_subscription_id = excluded.external_subscription_id,
            status = excluded.status,
            current_period_end = excluded.current_period_end,
            updated_at = excluded.updated_at
        """);
    // parameters via AddWithValue; nullables via DbValue(...)
    command.ExecuteNonQuery();
}
```

`GetSubscriptionByUser` / `GetSubscriptionByExternalSubscriptionId`: select by column, single-row read into `BillingSubscription`. `TryRecordBillingEvent`:

```csharp
using var command = _dataSource.CreateCommand("""
    insert into billing_webhook_events (event_id, processed_at)
    values (@event_id, @processed_at)
    on conflict (event_id) do nothing
    """);
...
return command.ExecuteNonQuery() == 1;
```

- [ ] **Step 5: Migration `013_billing.sql`** (and append the same tables to `outfit_planner_back/database/schema.sql` snapshot):

```sql
create table if not exists billing_subscriptions (
    user_id text primary key references users(id) on delete cascade,
    provider text not null,
    external_customer_id text not null,
    external_subscription_id text not null,
    status text not null,
    current_period_end timestamptz,
    updated_at timestamptz not null
);

create unique index if not exists ux_billing_subscriptions_external_subscription
    on billing_subscriptions (external_subscription_id);
create index if not exists ix_billing_subscriptions_external_customer
    on billing_subscriptions (external_customer_id);

create table if not exists billing_webhook_events (
    event_id text primary key,
    processed_at timestamptz not null
);
```

- [ ] **Step 6: Run backend tests — pass. Commit** `feat: billing subscription and webhook-event storage in all three stores`.

### Task 5: Stripe provider + Disabled provider

**Files:**
- Modify: `outfit_planner_back/src/OutfitPlanner.Infrastructure/OutfitPlanner.Infrastructure.csproj` (add `Stripe.net`)
- Create: `outfit_planner_back/src/OutfitPlanner.Infrastructure/Billing/StripeBillingProvider.cs`
- Create: `outfit_planner_back/src/OutfitPlanner.Infrastructure/Billing/DisabledBillingProvider.cs`

- [ ] **Step 1:** `dotnet add outfit_planner_back/src/OutfitPlanner.Infrastructure/OutfitPlanner.Infrastructure.csproj package Stripe.net` (latest stable). Then **inspect the resolved SDK surface** before writing the event mapping: check whether `Stripe.Subscription` exposes `CurrentPeriodEnd` directly or on `Items.Data[*]` (moved in newer Stripe API versions), and the exact `EventUtility.ConstructEvent` overload. Adjust the mapping accordingly — this is the one place the plan cannot pre-freeze code.

- [ ] **Step 2: `DisabledBillingProvider`:**

```csharp
using OutfitPlanner.Application.Abstractions;
using OutfitPlanner.Domain;

namespace OutfitPlanner.Infrastructure.Billing;

// Selected when no billing credentials are configured: status reads as disabled and
// every money-moving operation is rejected with the same validation surface as the app.
public sealed class DisabledBillingProvider : IBillingProvider
{
    public string Name => "disabled";
    public bool Enabled => false;
    public Task<string> CreateSubscriptionCheckoutAsync(UserAccount user, string priceId, string successUrl, string cancelUrl, CancellationToken cancellationToken)
        => throw new ValidationException("Billing is not configured.");
    public Task<string> CreateTopUpCheckoutAsync(UserAccount user, BillingTopUpPack pack, string successUrl, string cancelUrl, CancellationToken cancellationToken)
        => throw new ValidationException("Billing is not configured.");
    public Task<string> CreatePortalSessionAsync(string customerId, string returnUrl, CancellationToken cancellationToken)
        => throw new ValidationException("Billing is not configured.");
    public BillingWebhookEvent? ParseWebhookEvent(string payload, string? signatureHeader) => null;
}
```

(`ValidationException` lives in Application — confirm namespace via existing usages.)

- [ ] **Step 3: `StripeBillingProvider`** with `sealed record StripeBillingSettings(string SecretKey, string WebhookSecret)`; `StripeClient` field; `Name => "stripe"`, `Enabled => true`. Checkout sessions via `Stripe.Checkout.SessionService.CreateAsync`: subscription mode sets `ClientReferenceId = user.Id`, `CustomerEmail = user.Email`, one line item `{ Price = priceId, Quantity = 1 }`, `SubscriptionData.Metadata["userId"] = user.Id`, `Metadata { userId, type = "subscription" }`; payment mode sets `Metadata { userId, type = "top-up", packId = pack.Id, credits = pack.Credits.ToString() }` and line item `{ Price = pack.PriceId, Quantity = 1 }`. Portal via `Stripe.BillingPortal.SessionService.CreateAsync(new SessionCreateOptions { Customer = customerId, ReturnUrl = returnUrl })`. `ParseWebhookEvent`: `EventUtility.ConstructEvent(payload, signatureHeader, _settings.WebhookSecret, throwOnApiVersionMismatch: false)` in try/catch (`StripeException`/`ArgumentException` → null); map by `stripeEvent.Type` string literals:
  - `"checkout.session.completed"` → `Stripe.Checkout.Session`: `Kind = CheckoutCompleted`, `UserId = session.ClientReferenceId ?? metadata["userId"]`, `CustomerId = session.CustomerId`, `SubscriptionId = session.SubscriptionId`, `CheckoutMode = session.Mode`, and for payment mode `TopUpPackId = metadata["packId"]`, `TopUpCredits = int.TryParse(metadata["credits"])`.
  - `"customer.subscription.created"` and `"customer.subscription.updated"` → `Kind = SubscriptionUpdated` with `SubscriptionId = subscription.Id`, `CustomerId = subscription.CustomerId`, `Status = subscription.Status`, `UserId = subscription.Metadata["userId"]` (when present), `CurrentPeriodEnd` per Step 1 findings.
  - `"customer.subscription.deleted"` → `Kind = SubscriptionDeleted`, same fields.
  - anything else → `new BillingWebhookEvent(stripeEvent.Id, BillingWebhookEventKind.Ignored)`.

- [ ] **Step 4: Backend build + tests — pass. Commit** `feat: Stripe billing provider and disabled fallback`.

### Task 6: Api wiring — DI, config, endpoints, admin fields

**Files:**
- Modify: `outfit_planner_back/src/OutfitPlanner.Api/Program.cs` (DI ~163, store registrations ~330–370, endpoints after `/account/entitlements` ~761, `RequiresAuthenticatedUser` ~2192, `ToAdminUserResponse` ~2091, helpers near `LoadPlanCatalog` ~2123)
- Modify: `outfit_planner_back/src/OutfitPlanner.Api/Contracts/ApiContracts.cs`
- Modify: `outfit_planner_back/src/OutfitPlanner.Application/Abstractions/Repositories.cs` (`AdminUserRecord`)
- Modify: `outfit_planner_back/src/OutfitPlanner.Infrastructure/Storage/PostgresOutfitStore.cs` + `InMemoryOutfitStore.cs` (admin record subscription join/lookup)
- Modify: `outfit_planner_back/src/OutfitPlanner.Api/appsettings.json`, `appsettings.example.json` (empty placeholders)
- Test: `outfit_planner_back/tests/OutfitPlanner.Api.Tests/Program.cs`

- [ ] **Step 1: Failing api-level test.** Register `("api exposes billing endpoints", TestApiExposesBillingEndpoints)`; mirror `TestApiExposesPaywallEndpoints` (read it first for the harness): unauthenticated `GET /api/billing` → 401; after register+login: `GET /api/billing` → 200 with `"enabled":false` (no Stripe config in tests); `POST /api/billing/checkout` (CSRF header) → 400 "not configured"; `POST /api/billing/webhook` **without any auth cookies** → 400 (signature invalid — proves the route is anonymous, not 401); OpenAPI document contains `/api/billing` paths and `BillingStatusResponse` schema.

- [ ] **Step 2: DTOs** in `ApiContracts.cs`:

```csharp
public sealed record BillingSubscriptionResponse(string Status, DateTimeOffset? CurrentPeriodEnd, bool PremiumActive);

public sealed record BillingTopUpPackResponse(string Id, int Credits, string? DisplayPrice);

// Billing surface for the account: disabled billing keeps Enabled=false and empty packs
// so the UI can degrade to the ask-the-admin notice.
public sealed record BillingStatusResponse(
    bool Enabled,
    string Provider,
    bool SubscriptionPriceConfigured,
    string? PremiumDisplayPrice,
    BillingSubscriptionResponse? Subscription,
    IReadOnlyList<BillingTopUpPackResponse> TopUpPacks,
    bool PortalAvailable);

public sealed record BillingCheckoutResponse(string Url);

public sealed record StartTopUpCheckoutRequest(string PackId);
```

`AdminUserResponse` gains two trailing optional fields: `string? SubscriptionStatus = null, DateTimeOffset? SubscriptionPeriodEnd = null`.

- [ ] **Step 3: Admin record plumbing.** `AdminUserRecord` gains `string? SubscriptionStatus = null, DateTimeOffset? SubscriptionPeriodEnd = null`. InMemory `BuildAdminUserRecord`: look up `_subscriptions` for the user and pass status/period. Postgres: extend the admin list/detail SQL with `left join billing_subscriptions bs on bs.user_id = u.id` selecting `bs.status, bs.current_period_end` as two extra ordinals; extend `ReadAdminUserRecord`. `ToAdminUserResponse` passes both through.

- [ ] **Step 4: DI + selection + options** (helpers near `LoadPlanCatalog`):

```csharp
builder.Services.AddSingleton(LoadBillingOptions(builder.Configuration));
builder.Services.AddSingleton<IBillingProvider>(_ => CreateBillingProvider(builder.Configuration));
builder.Services.AddSingleton<BillingService>();
```

Register `ISubscriptionRepository`/`IBillingEventRepository` → the store singleton in all three storage branches (Postgres ~343, FileBacked ~357, and the in-memory branch). Helpers:

```csharp
static BillingOptions LoadBillingOptions(IConfiguration configuration)
{
    var origin = (configuration["Authentication:PublicOrigin"] ?? "").TrimEnd('/');
    var packs = configuration.GetSection("Stripe:TopUpPacks").GetChildren()
        .Select(section => new BillingTopUpPack(
            (section["Id"] ?? "").Trim(),
            int.TryParse(section["Credits"], out var credits) ? credits : 0,
            (section["PriceId"] ?? "").Trim(),
            NullIfWhiteSpace(section["DisplayPrice"])))
        .Where(pack => pack.Id.Length > 0 && pack.Credits > 0)
        .ToArray();
    return new BillingOptions(
        (configuration["Stripe:PremiumMonthlyPriceId"] ?? "").Trim(),
        NullIfWhiteSpace(configuration["Stripe:PremiumMonthlyDisplayPrice"]),
        packs,
        configuration["Stripe:SuccessUrl"] ?? $"{origin}/upgrade?checkout=success",
        configuration["Stripe:CancelUrl"] ?? $"{origin}/upgrade?checkout=cancelled",
        configuration["Stripe:PortalReturnUrl"] ?? $"{origin}/upgrade");

    static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

static IBillingProvider CreateBillingProvider(IConfiguration configuration)
{
    var configured = (configuration["Billing:Provider"] ?? "Auto").Trim().ToLowerInvariant();
    var secretKey = (configuration["Stripe:SecretKey"] ?? "").Trim();
    var webhookSecret = (configuration["Stripe:WebhookSecret"] ?? "").Trim();
    return configured switch
    {
        "stripe" => CreateStripeProvider(),
        "disabled" or "none" or "off" => new DisabledBillingProvider(),
        _ => string.IsNullOrWhiteSpace(secretKey) ? new DisabledBillingProvider() : CreateStripeProvider()
    };

    IBillingProvider CreateStripeProvider()
    {
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            throw new InvalidOperationException("Stripe:SecretKey must be configured when Billing:Provider selects Stripe.");
        }
        return new StripeBillingProvider(new StripeBillingSettings(secretKey, webhookSecret));
    }
}
```

- [ ] **Step 5: Endpoints** (after the entitlements endpoint) — GET status, POST checkout/topup/portal (try/catch `ValidationException` → 400, like neighbors, `.Produces<...>`), and the webhook:

```csharp
api.MapPost("/billing/webhook", async (HttpRequest request, BillingService billing, CancellationToken cancellationToken) =>
{
    using var reader = new StreamReader(request.Body);
    var payload = await reader.ReadToEndAsync(cancellationToken);
    try
    {
        var result = await billing.HandleWebhookAsync(payload, request.Headers["Stripe-Signature"], cancellationToken);
        return Results.Ok(new { status = result.Status });
    }
    catch (ValidationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});
```

`RequiresAuthenticatedUser` gains `&& !path.Equals("/billing/webhook", StringComparison.OrdinalIgnoreCase)` (webhook carries no session/CSRF; signature is its auth). `GET /api/billing` maps `BillingService.GetStatus` → `BillingStatusResponse`.

- [ ] **Step 6: appsettings placeholders** (values empty; secrets only via env) — add to both `appsettings.json` and `appsettings.example.json`:

```json
"Billing": { "Provider": "Auto" },
"Stripe": {
  "SecretKey": "",
  "WebhookSecret": "",
  "PremiumMonthlyPriceId": "",
  "PremiumMonthlyDisplayPrice": "",
  "SuccessUrl": "",
  "CancelUrl": "",
  "PortalReturnUrl": "",
  "TopUpPacks": [
    { "Id": "pack-20", "Credits": 20, "PriceId": "", "DisplayPrice": "" },
    { "Id": "pack-50", "Credits": 50, "PriceId": "", "DisplayPrice": "" },
    { "Id": "pack-100", "Credits": 100, "PriceId": "", "DisplayPrice": "" }
  ]
}
```

(Empty `SuccessUrl`/`CancelUrl`/`PortalReturnUrl` strings must fall back to the origin defaults — `LoadBillingOptions` uses `?? `, so change the reads to treat empty as missing: `NullIfWhiteSpace(configuration["Stripe:SuccessUrl"]) ?? $"{origin}/upgrade?checkout=success"`.)

- [ ] **Step 7: Backend tests + build — pass. Commit** `feat: billing API endpoints, provider selection, and admin subscription visibility`.

### Task 7: Frontend billing flow

**Files:**
- Modify: `outfit_planner_front/src/api/client.ts`, `src/types.ts`
- Create: `outfit_planner_front/src/routes/UpgradePage.tsx`, `src/routes/UpgradePage.test.tsx`, `src/features/billing/billing.css`
- Modify: `outfit_planner_front/src/app/App.tsx` (route), `src/app/AppShell.tsx` (AccountPanel billing block), `src/routes/BuilderPage.tsx:554-558` (notice link), `src/routes/AdminPage.tsx` (subscription column)

Order matters: run `npm run generate:api` (backend must build first) before typing against new endpoints.

- [ ] **Step 1: client functions** (`client.ts`, following the existing `request<T>` style):

```ts
export interface BillingSubscriptionInfo {
  status: string;
  currentPeriodEnd?: string | null;
  premiumActive: boolean;
}

export interface BillingTopUpPack {
  id: string;
  credits: number;
  displayPrice?: string | null;
}

export interface BillingStatus {
  enabled: boolean;
  provider: string;
  subscriptionPriceConfigured: boolean;
  premiumDisplayPrice?: string | null;
  subscription?: BillingSubscriptionInfo | null;
  topUpPacks: BillingTopUpPack[];
  portalAvailable: boolean;
}

export const billingStatusQueryKey = ['billing-status'] as const;

export function getBillingStatus(): Promise<BillingStatus> {
  return request<BillingStatus>('/billing');
}

export function startSubscriptionCheckout(): Promise<{ url: string }> {
  return request<{ url: string }>('/billing/checkout', { method: 'POST' });
}

export function startTopUpCheckout(packId: string): Promise<{ url: string }> {
  return request<{ url: string }>('/billing/topup', { method: 'POST', body: JSON.stringify({ packId }) });
}

export function openBillingPortal(): Promise<{ url: string }> {
  return request<{ url: string }>('/billing/portal', { method: 'POST' });
}
```

`AdminUser` gains `subscriptionStatus?: string | null; subscriptionPeriodEnd?: string | null;`.

- [ ] **Step 2: `UpgradePage`** (route `/upgrade` inside the `RequireAuth` block in `App.tsx`): `useQuery(billingStatusQueryKey, getBillingStatus)` + `useAuthSession()`; `useSearchParams` for `checkout=success|cancelled` notices (success → `queryClient.invalidateQueries` for session/entitlements/billing + "Payment received — your plan updates within a few seconds."); Premium plan card (editorial panel: unlimited wardrobe/outfits, all AI try-on modes, 4k output, 100 credits/month, priority queue; `premiumDisplayPrice` when set, otherwise "Price shown at checkout"); CTA `useMutation(startSubscriptionCheckout)` → `window.location.assign(data.url)`; top-up section listing `topUpPacks` with per-pack buy buttons (`startTopUpCheckout`) shown when the account's effective role is Premium; disabled-billing state keeps the current ask-the-admin copy; mutation errors render inline `<p className="error" role="alert">`. Follow `PageHeader` + `panel` conventions from `SharePage.tsx`/`AdminPage.tsx`; styles in `billing.css` reusing editorial tokens (no claymorphism).

- [ ] **Step 3: Surfaces.** BuilderPage upgrade notice becomes a `<Link to="/upgrade">See Premium plans</Link>` inside the existing `<p className="upgrade-notice">` (keep the text for context). AccountPanel: billing block after the gender field — current plan row (session role); when billing enabled and `portalAvailable`: `Manage subscription` button (`openBillingPortal` → redirect); when enabled and role Free: `<Link to="/upgrade">`. AdminPage: `Subscription` column rendering `subscriptionStatus ?? '—'` plus short period-end date.

- [ ] **Step 4: `UpgradePage.test.tsx`** (fetch-spy pattern from `BuilderPage.test.tsx`): renders plan card + packs from a mocked enabled billing status (Premium user sees pack buttons); disabled status shows the not-configured notice; `?checkout=success` shows the success notice.

- [ ] **Step 5:** `npm test` then `npm run build` (after backend build; sequential). **Commit** `feat: upgrade page, billing account block, and admin subscription column`.

### Task 8: Docs sync + graphify

**Files:** `PAYWALL_MODEL.md`, `README.md`, `CLAUDE.md`, `AGENTS.md`, spec/plan checkboxes.

- [ ] Update `PAYWALL_MODEL.md`: status header (stage 4 shipped to key-insertion readiness), trial 6→8 everywhere, the two deviations (lazy single-granting-authority instead of invoice-driven grants; trial top-up-to-config), the implemented data model (replace "not yet implemented"), webhook event list, config surface.
- [ ] README: features bullet + boundaries section (billing now wired, needs keys) + configuration table entries for `Billing__Provider`/`Stripe__*`.
- [ ] CLAUDE.md + AGENTS.md: update the paywall bullet **identically** (8 trial credits, billing stage 4 summary: provider selection, endpoints, webhook anonymity, role transitions honoring pinning, storage, frontend surfaces).
- [ ] Run `graphify update .`
- [ ] Commit `docs: paywall stage 4 (Stripe billing) and 8-credit trial`.

### Task 9: Full sequential verification

- [ ] Backend tests: `dotnet run --project outfit_planner_back/tests/OutfitPlanner.Api.Tests/OutfitPlanner.Api.Tests.csproj` → all pass.
- [ ] Backend build: `dotnet build outfit_planner_back/src/OutfitPlanner.Api/OutfitPlanner.Api.csproj` → 0 errors.
- [ ] Frontend: `cd outfit_planner_front && npm test` then `npm run build` (never overlapping backend builds).
- [ ] Browser sanity per the `outfit-planner-sequential-verification` skill and the dev-environment memory (HTTP `npx vite` + scratch `Storage__Local__DataPath`): `/upgrade` renders the disabled state, Builder notice links to it, account panel shows the plan row, admin table shows the Subscription column.
- [ ] `git diff --check` (CRLF CR-noise on CRLF files is expected, per repo memory).
- [ ] Report every command that could not run.

## Self-review

- Spec coverage: part 1 → Task 1; domain/application → Tasks 2–3; storage/migration → Task 4; Stripe/Disabled providers → Task 5; api/config/admin → Task 6; frontend → Task 7; docs/deviations → Task 8; verification → Task 9. Rollout checklist is configuration-only (no task needed). ✓
- Placeholders: the single deliberate open point is the Stripe.net SDK surface probe (Task 5 Step 1) — explicit verification step, not a TBD. ✓
- Type consistency: `BillingWebhookEventKind` names, `BillingService` ctor order `(users, subscriptions, events, credits, provider, options, rolePinning, clock)`, `BillingTopUpPack(Id, Credits, PriceId, DisplayPrice)`, response DTO names — cross-checked across tasks. ✓
