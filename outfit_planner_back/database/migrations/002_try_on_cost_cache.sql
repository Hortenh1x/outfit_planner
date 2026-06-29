alter table try_on_jobs add column if not exists try_on_mode text not null default 'SequentialOutfitTryOn';
alter table try_on_jobs add column if not exists confirmed_credits integer not null default 0;
alter table try_on_jobs add column if not exists cache_key text;
alter table try_on_jobs add column if not exists served_from_cache boolean not null default false;
alter table try_on_jobs add column if not exists source_cached_job_id uuid references try_on_jobs(id) on delete set null;
alter table try_on_jobs add column if not exists provider_settings_hash text;

create index if not exists ix_try_on_jobs_user_cache_succeeded
on try_on_jobs (user_id, cache_key, created_at desc)
where status = 'Succeeded' and output_image_url is not null and is_deleted = false;
