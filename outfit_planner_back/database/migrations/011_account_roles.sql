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

-- Pinned accounts. The API also enforces these roles at read time by normalized email
-- (role-pinning policy), so this backfill is convergence, not the only guarantee.
update users set role = 'Admin' where normalized_email = 'dmytro.bolibok@gmail.com' and role <> 'Admin';
update users set role = 'Premium' where normalized_email = 'olya.shaydur@gmail.com' and role <> 'Premium';
