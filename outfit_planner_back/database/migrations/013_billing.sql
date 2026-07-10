-- Stage-4 billing (PAYWALL_MODEL.md): provider-owned subscription state mirrored
-- locally, plus the webhook idempotency ledger (an event id is processed at most once).
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
