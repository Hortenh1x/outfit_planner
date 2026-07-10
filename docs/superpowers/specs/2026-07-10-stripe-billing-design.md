# Stripe Billing + Trial Credit Raise — Design

Date: 2026-07-10. Implements paywall stage 4 of `PAYWALL_MODEL.md` ("insert the API key"
readiness) plus the trial-credit raise to 8. Approved direction: the user requested both
parts after the gap assessment; interactive brainstorming was compressed because the
design substrate already exists in `PAYWALL_MODEL.md` and the account is operating
autonomously. Product decisions taken here follow that document; deviations are called
out explicitly.

## Goals

1. Every Free account has **8 trial AI credits** (= 4 single 1k runs), including accounts
   that already received the old 6-credit grant.
2. **Stripe billing implemented end-to-end** so that configuring `Stripe__SecretKey`,
   `Stripe__WebhookSecret`, and price IDs is the only remaining step to sell Premium:
   subscription checkout, top-up checkout, customer portal, webhook-driven subscription
   state and `Free ↔ Premium` role transitions, admin visibility, and the frontend
   upgrade flow. Without credentials everything degrades softly (billing reads as
   disabled; nothing else changes).

## Part 1 — Trial credits: 8, top-up-to-config semantics

- `PlanCatalog.Default` Free `TrialCredits: 6 → 8` (`Entitlements.cs`). Config override
  stays `Paywall__Free__TrialCredits`.
- `CreditLedgerService.EnsureGrants` changes from "grant once if never granted" to
  **top-up-to-config**: sum of existing `TrialGrant` entries `granted`; if
  `limits.TrialCredits > granted`, append a `TrialGrant` entry for the difference.
  - New accounts get one 8-credit grant. Accounts that got 6 get a +2 entry on their next
    balance read (lazy, idempotent, works in all three stores, no migration).
  - Lowering the config later never claws back (only positive differences grant).
  - Spends don't interfere: the check sums only `TrialGrant` rows, not the balance.
- New repository method `int GetCreditSumByReason(string userId, CreditLedgerReason reason)`
  on `ICreditLedgerRepository`, implemented in Postgres (`COALESCE(SUM(delta),0)` filtered
  by reason), InMemory (LINQ sum), FileBacked (delegate). The monthly grant keeps using
  `HasCreditEntryWithReasonSince`.
- Docs: `PAYWALL_MODEL.md` (6 → 8, deviation note), README, CLAUDE.md + AGENTS.md bullets.

## Part 2 — Stripe billing

### Architecture (onion-preserving)

- **Domain**: `BillingSubscription` record (`UserId`, `Provider`, `ExternalCustomerId`,
  `ExternalSubscriptionId`, `Status` — normalized lowercase provider status string,
  `CurrentPeriodEnd?`, `UpdatedAt`) and `BillingRules.GrantsPremium(status)`
  (`active | trialing | past_due` → Premium; everything else → not). `past_due` keeps
  Premium as a grace window while Stripe retries payment.
- **Application**:
  - `ISubscriptionRepository`: `UpsertSubscription`, `GetSubscriptionByUser`,
    `GetSubscriptionByExternalSubscriptionId`.
  - `IBillingEventRepository`: `bool TryRecordBillingEvent(string eventId, DateTimeOffset processedAt)`
    — atomic first-time-wins; the webhook idempotency gate.
  - `IBillingProvider`: `Name`, `Enabled`,
    `CreateSubscriptionCheckoutAsync(user, successUrl, cancelUrl, ct)`,
    `CreateTopUpCheckoutAsync(user, pack, successUrl, cancelUrl, ct)`,
    `CreatePortalSessionAsync(customerId, returnUrl, ct)`,
    `ParseWebhookEvent(payload, signatureHeader)` → normalized `BillingWebhookEvent?`
    (null/throw = invalid signature → 400).
  - `BillingWebhookEvent` (provider-agnostic): `EventId`, `Kind`
    (`CheckoutCompleted | SubscriptionUpdated | SubscriptionDeleted | Ignored`), and
    nullable payload fields: `UserId`, `CustomerId`, `SubscriptionId`, `Status`,
    `CurrentPeriodEnd`, `CheckoutMode` (`subscription | payment`), `TopUpPackId`,
    `TopUpCredits`.
  - `BillingOptions` (built by Api from config): `PremiumPriceId`,
    `PremiumDisplayPrice?`, `IReadOnlyList<BillingTopUpPack>`
    (`Id`, `Credits`, `PriceId`, `DisplayPrice?`), success/cancel/return URLs.
    Provider price IDs are opaque strings; packs with an empty `PriceId` are not offered.
  - `BillingService` use cases:
    - `GetStatus(userId)` → enabled flag, provider name, current subscription
      (status, period end, `GrantsPremium`), offered packs, premium price display,
      `PortalAvailable`.
    - `StartSubscriptionCheckoutAsync`: effective role must be `Free` (Premium → "manage
      in the portal", Admin → not sellable); requires configured premium price.
    - `StartTopUpCheckoutAsync(packId)`: effective role must be `Premium`
      (`PAYWALL_MODEL.md` sells top-ups as a Premium feature; Admin has unlimited
      credits and is rejected).
    - `CreatePortalAsync`: requires an existing subscription row (its customer id).
    - `HandleWebhookAsync(payload, signature)`: parse → invalid = `ValidationException`;
      `TryRecordBillingEvent` false → `duplicate` (200, no-op); then:
      - `CheckoutCompleted(mode=subscription)`: bind user↔customer↔subscription (upsert
        with status `active` optimistically; follow-up subscription events correct), then
        run the role transition immediately — the paying user must not wait for the next
        subscription event to become Premium.
      - `SubscriptionUpdated/Deleted`: resolve user by event `UserId` (subscription
        metadata) or store lookup by subscription id; upsert status/period
        (`Deleted` → `canceled`); then role transition.
      - `CheckoutCompleted(mode=payment)` (top-up): `CreditLedgerService.GrantTopUp(userId,
        credits)` with credits from session metadata (written server-side at session
        creation), sanity-bounded (1..10000); idempotency rides the event gate.
    - **Role transitions**: stored role flips only between `Free` and `Premium`
      (`GrantsPremium(status)`); pinned accounts (`RolePinningPolicy.PinnedRole != null`)
      and stored-`Admin` accounts are never rewritten by webhooks. Written via
      `IUserAccountRepository.UpdateUser(user with { Role, UpdatedAt })`, mirroring the
      sign-in fold.
  - `CreditLedgerService.GrantTopUp(userId, credits)`: appends a `TopUp` ledger entry
    (reason already exists in the enum), validation 1..10000.
- **Infrastructure**:
  - `Billing/StripeBillingProvider` over the `Stripe.net` NuGet package: Checkout
    Sessions (`mode=subscription` with `client_reference_id = userId` **and**
    `subscription_data.metadata[userId]`; `mode=payment` with metadata
    `type=top-up, userId, packId, credits`), Billing Portal sessions, and
    `EventUtility.ConstructEvent` (signature verification, tolerant of API version
    drift) mapping `checkout.session.completed`, `customer.subscription.created`,
    `customer.subscription.updated` (both → `SubscriptionUpdated` — a fresh subscription
    may only emit `created`), and `customer.subscription.deleted` to
    `BillingWebhookEvent`. All other event types map to `Ignored` (recorded, 200).
  - **Deviation from the `PAYWALL_MODEL.md` sketch**: `invoice.paid` is not handled and
    webhooks never write credit-grant ledger rows for subscriptions. The existing lazy
    once-per-UTC-month grant in `CreditLedgerService` stays the **single granting
    authority**, driven by the effective Premium role that the webhook flips. This kills
    the double-grant window (lazy + invoice-driven), keeps pinned-Premium accounts
    working without billing, and `customer.subscription.updated` already delivers the
    renewed `current_period_end`. Consequence (documented): a user who cancels keeps
    rolled-over credits — already the shipped rollover semantic.
  - `DisabledBillingProvider`: `Enabled=false`; checkout/portal throw
    `ValidationException("Billing is not configured.")`.
  - Storage in all three stores: `billing_subscriptions` (PK `user_id`, FK cascade,
    unique `external_subscription_id`) and `billing_webhook_events` (PK `event_id`) —
    migration `013_billing.sql` + `database/schema.sql` snapshot; InMemory dictionaries +
    snapshot fields (nullable-with-default like `CreditLedger`); FileBacked delegates +
    `SaveSnapshot()` on mutations.
- **Api**:
  - Provider selection mirrors try-on/background-removal: `Billing__Provider =
    Auto | Stripe | Disabled` (default `Auto` = Stripe when `Stripe__SecretKey` is set).
  - Config surface (all placeholders empty in `appsettings.json`; secrets only via env):
    `Stripe__SecretKey`, `Stripe__WebhookSecret`, `Stripe__PremiumMonthlyPriceId`,
    `Stripe__PremiumMonthlyDisplayPrice`, `Stripe__TopUpPacks__N__{Id,Credits,PriceId,DisplayPrice}`,
    optional `Stripe__SuccessUrl`/`Stripe__CancelUrl`/`Stripe__PortalReturnUrl`
    (default: `Authentication__PublicOrigin` + `/upgrade?checkout=success|cancelled` and
    `/upgrade`). Default packs ship as 20/50/100 credits with empty price IDs
    (not offered until priced).
  - Endpoints: `GET /api/billing` (status DTO), `POST /api/billing/checkout`,
    `POST /api/billing/topup { packId }`, `POST /api/billing/portal` (all session+CSRF
    authenticated, `ValidationException → 400`), and `POST /api/billing/webhook` —
    **anonymous** (added to the `RequiresAuthenticatedUser` allowlist, so no session/CSRF
    applies), reads the raw body, verifies the `Stripe-Signature` header, 400 on invalid
    signature, 200 `{ status: processed | duplicate | ignored }` otherwise.
  - Admin: `AdminUserRecord` + `AdminUserResponse` gain nullable
    `SubscriptionStatus` / `SubscriptionPeriodEnd`, populated via a LEFT JOIN in the
    Postgres admin list/detail queries and a dictionary lookup in InMemory — read-only.
- **Frontend**:
  - `client.ts`: `BillingStatus` types + `getBillingStatus` (`['billing']` query key),
    `startSubscriptionCheckout`, `startTopUpCheckout(packId)`, `openBillingPortal`;
    `AdminUser` gains the two subscription fields.
  - New authenticated route `/upgrade` (`UpgradePage`): editorial page with the Premium
    plan card (unlimited wardrobe/outfits, all AI modes, 4k, 100 credits/month, priority
    queue; display price when configured), checkout CTA that redirects to the returned
    Stripe URL (`window.location.assign`), a top-up packs section (Premium accounts),
    `?checkout=success|cancelled` notices (success invalidates session/entitlements/
    billing queries and explains that webhooks land within seconds), and a graceful
    "billing is not configured" state that keeps today's ask-the-admin text.
  - Builder upgrade notice links to `/upgrade`; `AccountPanel` gains a Billing block
    (current plan; `Manage subscription` → portal redirect for subscribed Premium;
    `See Premium` link for Free; hidden when billing is disabled except the plan row);
    AdminPage table gains a read-only Subscription column.
- **Explicitly out of scope**: Stripe Tax/invoicing config, proration strategy, webhook
  retry queues beyond idempotent 200s, Wardrobe cap-notice link (text already mentions
  Premium), grandfathering copy, real prices (placeholders remain until pricing
  validation).

### Error handling

- Invalid webhook signature → 400 with no side effects; unknown/unmapped events → 200
  `ignored` after recording the event id (Stripe stops retrying).
- Webhook handlers are defensive: missing user/subscription resolution logs and returns
  200 `ignored` (a later event with better data converges state); nothing throws for
  data reasons.
- Checkout/portal endpoints surface `ValidationException` messages as 400s (existing
  convention); the frontend shows them inline like other mutations.
- Provider API failures (Stripe down) bubble as 500 via the existing unhandled-error
  envelope; the UI mutation error state covers it.

### Testing

- Backend console tests: updated credit-ledger numbers (8); new tests for trial top-up
  semantics; `BillingRules` status mapping; `BillingService` webhook flow with a fake
  provider (checkout → subscription active → stored role Premium + monthly grant on next
  read; deleted → Free; duplicate event id no-ops; top-up grants once; pinned account
  stored role untouched; portal requires a subscription; Free-only checkout guard);
  schema assertions for the billing tables; api-level test that the billing endpoints
  exist, the webhook is anonymous-but-signature-guarded, and OpenAPI documents the DTOs.
- Frontend: `UpgradePage` vitest (renders plan card from a mocked billing status; shows
  the disabled state; success query param renders the notice); client function typing
  compiles via generated OpenAPI.
- Full sequential verification per `outfit-planner-sequential-verification` (backend
  test → backend build → frontend test → frontend build → browser sanity → `git diff
  --check`).

### Rollout / config to flip it on (the "insert the key" checklist)

1. Create a Stripe product with a monthly price (Premium) and one product per top-up pack.
2. Set `Stripe__SecretKey`, `Stripe__WebhookSecret` (from the dashboard webhook endpoint
   pointing at `https://<host>/api/billing/webhook` with the four event types:
   `checkout.session.completed`, `customer.subscription.created`,
   `customer.subscription.updated`, `customer.subscription.deleted`),
   `Stripe__PremiumMonthlyPriceId`, and pack price IDs — via environment/compose, never
   committed.
3. Optionally set display prices. Done: Free accounts see the upgrade CTA, checkout,
   webhook role flips, portal, and top-ups.
