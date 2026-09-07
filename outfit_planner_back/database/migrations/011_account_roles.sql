alter table users add column if not exists role text not null default 'Free';

do $$
begin
    if not exists (
        select 1
        from pg_constraint
        where conname = 'ck_users_role'
    ) then
        alter table users
            add constraint ck_users_role
            check (role in ('Free', 'Premium', 'Admin'));
    end if;
end $$;

-- Pinned admin account. The API also enforces pinned roles at read time by normalized email
-- (role-pinning policy), so this backfill is convergence, not the only guarantee. Premium
-- pins are configuration-only (Roles__PinnedPremiumEmails) and converge on sign-in.
update users set role = 'Admin' where normalized_email = 'dmytro.bolibok@gmail.com' and role <> 'Admin';
