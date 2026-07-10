# Paywall Model

Status: **stages 1–4 implemented**. Roles/pinning/admin panel, the `PlanCatalog`
entitlements, the `account_credit_ledger` metering (trial + monthly grants, debit on
confirm, refund on failure), tier-gated try-on modes, the per-tier resolution repricing,
the entitlements UI, and **Stripe billing to "insert the API key" readiness** (design:
`docs/superpowers/specs/2026-07-10-stripe-billing-design.md`): checkout for the
subscription and top-up packs, the customer portal, signature-verified idempotent
webhooks that mirror subscription state and flip stored `Free ↔ Premium` roles, admin
subscription visibility, and the `/upgrade` frontend flow. No billing credentials are
committed — with `Stripe__SecretKey` unset, billing reads as disabled and everything else
works unchanged. Numbers marked *(placeholder)* still need pricing validation and are
configurable through `Paywall__Free__*` / `Paywall__Premium__*` / `Stripe__*`.

Implementation deviations from the original sketch:

- Monthly grants are granted once per UTC calendar month and **roll over** (stored
  without expiry) instead of expiring — expiring grants with non-expiring spends would
  let balances drift negative across months. The `expires_at` column exists and is
  honored by the balance query, reserved for a future tightening.
- The trial grant is **top-up-to-config**: the ledger grants the difference whenever the
  configured trial exceeds the sum of a user's `TrialGrant` rows, so raising the trial
  (6 → 8) also tops up already-granted accounts on their next balance read; lowering it
  never claws back.
- Webhooks never write credit-grant rows and `invoice.paid` is not handled: the lazy
  monthly grant in `CreditLedgerService` stays the **single granting authority**, driven
  by the effective Premium role the webhook flips. This kills the lazy-plus-invoice
  double-grant window and keeps pinned-Premium accounts working without billing;
  `customer.subscription.updated` already delivers renewed period ends. Top-ups are the
  exception: a `checkout.session.completed` (payment mode) appends a `TopUp` ledger row
  from server-stamped session metadata, idempotent via the webhook event gate.

## Philosophy

Wardrobe organization is the habit loop and stays free: cataloging garments, composing
outfits on the figure, planning the calendar, sharing looks. What costs the project real
money is AI compute — FASHN `tryon-max` runs are credit-priced per generation — so the
paywall meters AI generation, not organization.

## What the backend already provides (enforcement primitives)

The existing try-on pipeline was built estimate-first, which is exactly the shape a
paywall needs:

- `POST /api/outfits/{id}/try-on/estimate` returns mode availability, credits, cache key;
  `POST /api/outfits/{id}/try-on` must echo the server estimate and the server recomputes
  and rejects stale/mismatched confirmations. Nothing spends without a confirmed estimate.
- Provider capabilities already price runs: FASHN `tryon-max` quality credits follow output
  resolution (`1k` = 2 credits, `4k` = 5 per run; `SequentialOutfitTryOn` = one run per
  body garment), `ExperimentalCompositeTryOn` = 1 credit and is already flagged
  `RequiresPremiumConfirmation`.
- Cache hits must not enqueue provider work or call AI — repeat generations are free by
  construction, so users are never double-charged for the same outfit/body/settings.
- `ClothesOnlyPreview` is free and provider-less; `Shoes`/`Bag`/`Accessory` never reach AI
  outside the composite mode.
- Sessions now carry the effective account role (`Free`/`Premium`/`Admin`, with the two
  pinned accounts), and the admin panel can inspect/manage every account.
- Try-on endpoints are rate-limited per session; auth endpoints per IP.

## Tiers

### Free (default for every account)

- Wardrobe, Builder composed figure, Calendar, sharing: included.
- Caps *(placeholders)*: 50 garments, 20 saved outfits, 1 body reference photo.
- Background removal and auto-tag suggestions: included (local/cheap).
- AI try-on: one-time **trial grant of 8 credits** *(placeholder — 4 × 1k single runs;
  raised from 6, existing accounts top up to the configured amount lazily)*;
  `SingleGarmentTryOn` only, output resolution capped at `1k`, standard queue.
- No `SequentialOutfitTryOn`, no `ExperimentalCompositeTryOn`, no `4k`.

### Premium (subscription)

- Everything uncapped that Free caps: garments, outfits, body reference photos
  (up to 5 *(placeholder)* — they are sensitive data, so still bounded).
- Monthly allowance of **100 AI credits** *(placeholder)*, expiring monthly, plus one-off
  credit top-up purchases that never expire.
- All modes: `SingleGarmentTryOn`, `SequentialOutfitTryOn`, `ExperimentalCompositeTryOn`.
- `4k` output resolution, priority position in the try-on queue.
- First in line for future premium capabilities (multi-garment detection, premium
  background removal / auto-tagging providers).

### Admin (internal)

- Not sellable. Bypasses caps and allowances, sees the admin panel. Pinned to
  `dmytro.bolibok@gmail.com`; `olya.shaydur@gmail.com` is pinned Premium and doubles as a
  standing test account for premium behavior without billing.

## Credit economics

App credits map 1:1 to FASHN credits so the cost basis is transparent: a `1k` run debits
2, a `4k` run debits 5, a composite run debits 1, and a sequential outfit debits
per-garment (e.g. top+bottom at 4k = 10). Price the Premium subscription so the monthly
allowance covers its FASHN cost with margin, and price top-ups strictly above marginal
FASHN cost. Failed provider runs are refunded automatically; cache hits never debit.

## Data model (implemented — migrations 012 and 013)

```sql
create table account_credit_ledger (           -- migration 012
    id uuid primary key,
    user_id text not null references users(id) on delete cascade,
    delta integer not null,                    -- positive grant, negative spend
    reason text not null check (reason in     -- stored as enum names
        ('TrialGrant', 'SubscriptionGrant', 'TopUp', 'TryOnSpend', 'Refund', 'AdminAdjustment')),
    try_on_job_id uuid,                        -- set for spends/refunds
    expires_at timestamptz,                    -- reserved; current grants roll over
    created_at timestamptz not null default now()
);

create table billing_subscriptions (           -- migration 013
    user_id text primary key references users(id) on delete cascade,
    provider text not null,
    external_customer_id text not null,
    external_subscription_id text not null,    -- unique index
    status text not null,                      -- normalized lowercase provider status
    current_period_end timestamptz,
    updated_at timestamptz not null
);

create table billing_webhook_events (          -- migration 013: idempotency gate
    event_id text primary key,
    processed_at timestamptz not null
);
```

Balance = sum of non-expired ledger rows. The ledger is append-only, so admin disputes and
refunds are auditable; `AdminAdjustment` gives the admin panel a manual lever. A webhook
event id is processed at most once (`billing_webhook_events` first-time-wins insert), so
Stripe replays and retries are no-ops. All three stores (Postgres, in-memory, file-backed
snapshot) implement `ISubscriptionRepository` + `IBillingEventRepository`.

## Where enforcement plugs in

| Rule | Enforcement point |
| --- | --- |
| Mode availability per tier | `TryOnCostEstimator` — reuse the existing `IsAvailable` / `RequiresPremiumConfirmation` / `Warnings` fields, driven by a `PlanCatalog` (role → entitlements) in Application |
| Credit balance check + debit | try-on confirm endpoint: recompute estimate, check ledger balance, debit atomically when the job is accepted; refund on `Failed` transition in the background worker |
| No spend on cache hits | already guaranteed — cache hits return without provider work |
| Resolution cap (Free = `1k`) | per-request provider generation settings override; today `Fashn__Resolution` is a global setting, so this needs the estimator/provider request to carry the tier's resolution (the request/capabilities plumbing already exists) |
| Garment/outfit/body-photo caps | create paths in `WardrobeService` / `OutfitService` — count + `ValidationException` with an upgrade-friendly message |
| Priority queue | queue entry metadata; the Redis list can become two lists (premium first) without changing the worker contract |
| UI gating | session already carries `role`; gated actions open an upgrade dialog instead of silently failing — estimates already return per-mode availability for honest buttons |

Everything decision-shaped lives behind one `PlanCatalog`/entitlements resolver so tiers
are data, not scattered `if (role == ...)` checks. Roles stay the coarse switch; the
ledger meters consumption.

## Billing integration (implemented, Stripe)

1. Stripe Checkout for the subscription (`POST /api/billing/checkout`, effective-Free
   accounts only) and top-up packs (`POST /api/billing/topup`, effective-Premium only);
   the customer portal (`POST /api/billing/portal`) for self-service cancel/upgrade. No
   card data ever touches the API. `GET /api/billing` reports enablement, the current
   subscription, and the offered packs.
2. Webhook `POST /api/billing/webhook` is **anonymous by design** (allowlisted in
   `RequiresAuthenticatedUser` — the `Stripe-Signature` check is the authentication) and
   handles `checkout.session.completed`, `customer.subscription.created`,
   `customer.subscription.updated`, and `customer.subscription.deleted` (a fresh
   subscription may only emit `created`; `invoice.paid` is deliberately not handled — see
   the deviations above). Handlers upsert `billing_subscriptions` and flip stored roles
   per `BillingRules.GrantsPremium` (`active`/`trialing`/`past_due`; `past_due` is the
   grace window). Pinned accounts and stored admins are never rewritten by webhooks (the
   pinning policy also guarantees this at read time). Unresolvable events return
   `ignored`, not errors — a later event converges the state.
3. Webhook handlers are idempotent (`billing_webhook_events` event-id gate) and tolerate
   replays; invalid signatures are 400s with no side effects.
4. The admin panel shows a read-only Subscription column (status, period end) next to the
   manual `AdminAdjustment` credit lever.
5. Provider selection: `Billing__Provider=Auto|Stripe|Disabled` (default `Auto` = Stripe
   when `Stripe__SecretKey` is set, softly disabled otherwise). Config:
   `Stripe__SecretKey`, `Stripe__WebhookSecret`, `Stripe__PremiumMonthlyPriceId`,
   `Stripe__PremiumMonthlyDisplayPrice`, `Stripe__TopUpPacks__N__{Id,Credits,PriceId,DisplayPrice}`
   (packs without a `PriceId` are not offered), and optional
   `Stripe__SuccessUrl`/`Stripe__CancelUrl`/`Stripe__PortalReturnUrl` (default:
   `Authentication__PublicOrigin` + `/upgrade?checkout=success|cancelled` and `/upgrade`).
6. Frontend: `/upgrade` (plan card, checkout redirect, Premium top-up packs,
   `?checkout=success|cancelled` notices, graceful disabled state), the Builder upgrade
   notice links there, account settings shows a Plan row with the portal for subscribed
   Premium accounts.

### Turning it on (the "insert the key" checklist)

1. Create the Stripe product/price for Premium monthly and one price per top-up pack.
2. Point a Stripe webhook at `https://<host>/api/billing/webhook` with the four event
   types above; note its signing secret.
3. Set `Stripe__SecretKey`, `Stripe__WebhookSecret`, `Stripe__PremiumMonthlyPriceId`, and
   pack price ids via environment/compose (never committed). Optionally set display
   prices. Done.

## Rollout stages

1. **Foundation** — roles + pinned accounts + admin panel. *(shipped)*
2. **Visible entitlements** — estimates and Builder UI show per-tier availability and an
   upgrade notice; Free sees which modes are Premium. *(shipped: `GET
   /api/account/entitlements`, Premium pills on gated modes, credits chip, upgrade notice)*
3. **Metering** — credit ledger + trial/monthly grants + debit/refund wiring + admin
   credit adjustments. *(shipped: migration 012, `CreditLedgerService`, `POST
   /api/admin/users/{id}/credits`)*
4. **Billing** — Stripe subscription/top-ups + webhook role transitions. *(shipped to
   key-insertion readiness: migration 013, `BillingService`, `StripeBillingProvider`,
   `/upgrade` flow; awaiting Stripe credentials + real prices)*
5. **Tighten Free caps** — the caps are enforced (garments/outfits/body photos) with the
   placeholder numbers; revisit the numbers and grandfathering before charging money.
   *(enforcement shipped, numbers provisional)*

## What stays free forever

Composed-figure previews, wardrobe cataloging with background removal, calendar planning,
and share links. The paywall should never make the core organize–compose–plan loop feel
rented; it sells compute (AI generations) and scale (uncapped wardrobe).

## Risks and guardrails

- **Cost control:** per-session try-on rate limiting exists; add a global daily FASHN
  spend cap (config) that flips AI modes to "temporarily unavailable" instead of
  overspending.
- **Trial abuse:** trial credits are small, granted once per verified email; registration
  is already rate-limited per IP.
- **Refund correctness:** debit on acceptance + automatic `refund` ledger row on `Failed`
  keeps user trust; cached results already cost nothing.
- **Provider swap:** credits are defined by provider capabilities, so a cheaper provider
  changes economics without changing the model.
