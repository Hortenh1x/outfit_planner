create table if not exists account_credit_ledger (
    id uuid primary key,
    user_id text not null references users(id) on delete cascade,
    delta integer not null,
    reason text not null check (reason in ('TrialGrant', 'SubscriptionGrant', 'TopUp', 'TryOnSpend', 'Refund', 'AdminAdjustment')),
    -- No FK: spend rows are written just before their job row to keep debits ahead of queueing.
    try_on_job_id uuid,
    -- Reserved for future expiring grants; current grants roll over (null).
    expires_at timestamptz,
    created_at timestamptz not null default now()
);

create index if not exists ix_account_credit_ledger_user_id on account_credit_ledger (user_id);
create index if not exists ix_account_credit_ledger_job on account_credit_ledger (try_on_job_id) where try_on_job_id is not null;
