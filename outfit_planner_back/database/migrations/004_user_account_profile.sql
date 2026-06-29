alter table users add column if not exists avatar_url text;
alter table users add column if not exists avatar_object_key text;
alter table users add column if not exists gender text;

do $$
begin
    if not exists (
        select 1
        from pg_constraint
        where conname = 'ck_users_gender'
    ) then
        alter table users
            add constraint ck_users_gender
            check (gender is null or gender in ('Male', 'Female'));
    end if;
end $$;
