# Paywall Model

Status: **stages 1–3 implemented** (plus the free-tier caps, the premium priority queue,
and the admin credit lever). Roles/pinning/admin panel, the `PlanCatalog` entitlements,
the `account_credit_ledger` metering (trial + monthly grants, debit on confirm, refund on
failure), tier-gated try-on modes, the per-tier resolution repricing, and the entitlements
UI are live. **Stage 4 (Stripe billing) is intentionally not implemented** — there are no
billing credentials; the `Free ↔ Premium` role switch and the ledger are billing-ready.
Numbers marked *(placeholder)* still need pricing validation and are configurable through
`Paywall__Free__*` / `Paywall__Premium__*`.

Implementation deviation from the original sketch: monthly grants are granted once per UTC
calendar month and **roll over** (stored without expiry) instead of expiring — expiring
grants with non-expiring spends would let balances drift negative across months. The
`expires_at` column exists and is honored by the balance query, reserved for a future
tightening.

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
- AI try-on: one-time **trial grant of 6 credits** *(placeholder — 3 × 1k single runs)*;
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

## Data model sketch (not yet implemented)

```sql
create table account_credit_ledger (
    id uuid primary key,
    user_id text not null references users(id) on delete cascade,
    delta integer not null,                    -- positive grant, negative spend
    reason text not null check (reason in
        ('trial-grant', 'subscription-grant', 'top-up', 'try-on-spend', 'refund', 'admin-adjustment')),
    try_on_job_id uuid,                        -- set for spends/refunds
    expires_at timestamptz,                    -- monthly grants expire; top-ups do not
    created_at timestamptz not null default now()
);

create table subscriptions (
    user_id text primary key references users(id) on delete cascade,
    provider text not null default 'stripe',
    external_customer_id text not null,
    external_subscription_id text not null,
    status text not null,                      -- active / past_due / canceled
    current_period_end timestamptz not null,
    updated_at timestamptz not null default now()
);
```

Balance = sum of non-expired ledger rows. The ledger is append-only, so admin disputes and
refunds are auditable; `admin-adjustment` gives the admin panel a manual lever.

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

## Billing integration path (Stripe-shaped)

1. Stripe Checkout for subscription + top-ups; Stripe customer portal for self-service
   cancel/upgrade (no card data ever touches the API).
2. Webhooks (`checkout.session.completed`, `invoice.paid`,
   `customer.subscription.updated/deleted`) drive: subscription row upsert, monthly
   `subscription-grant` ledger rows, and the `Free ↔ Premium` stored-role transition.
   Pinned accounts are exempt from webhook-driven role changes (the pinning policy already
   guarantees this at read time).
3. Webhook handlers must be idempotent (event id dedup table) and tolerate replays.
4. Admin panel gains a read-only billing column (status, period end) and the manual
   `admin-adjustment` credit lever for support.

## Rollout stages

1. **Foundation** — roles + pinned accounts + admin panel. *(shipped)*
2. **Visible entitlements** — estimates and Builder UI show per-tier availability and an
   upgrade notice; Free sees which modes are Premium. *(shipped: `GET
   /api/account/entitlements`, Premium pills on gated modes, credits chip, upgrade notice)*
3. **Metering** — credit ledger + trial/monthly grants + debit/refund wiring + admin
   credit adjustments. *(shipped: migration 012, `CreditLedgerService`, `POST
   /api/admin/users/{id}/credits`)*
4. **Billing** — Stripe subscription/top-ups + webhook role transitions. *(not started —
   requires Stripe credentials)*
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
