-- Indexes for the per-user try-on history listing, expired-session cleanup, and the
-- foreign-key child columns that cascade deletes scan. Postgres does not auto-index FK
-- columns, so account/outfit/garment deletes would otherwise sequentially scan children.
-- All statements are idempotent (create index if not exists), so re-running is safe.

-- Try-on history: select ... from try_on_jobs where user_id = @user_id order by created_at desc
create index if not exists ix_try_on_jobs_user_created_at
on try_on_jobs (user_id, created_at desc);

-- Expired session cleanup: delete from auth_sessions where expires_at <= @now
create index if not exists ix_auth_sessions_expires_at
on auth_sessions (expires_at);

-- Foreign-key child columns (cascade / set null on parent delete).
create index if not exists ix_outfit_items_outfit_id
on outfit_items (outfit_id);

create index if not exists ix_outfit_items_garment_id
on outfit_items (garment_id);

create index if not exists ix_scheduled_outfits_outfit_id
on scheduled_outfits (outfit_id);

create index if not exists ix_try_on_jobs_outfit_id
on try_on_jobs (outfit_id);

create index if not exists ix_try_on_jobs_source_body_photo_id
on try_on_jobs (source_body_photo_id);

create index if not exists ix_try_on_jobs_source_cached_job_id
on try_on_jobs (source_cached_job_id);

create index if not exists ix_share_links_user_id
on share_links (user_id);

create index if not exists ix_share_links_outfit_id
on share_links (outfit_id);

create index if not exists ix_body_reference_photos_user_id
on body_reference_photos (user_id);

create index if not exists ix_auth_email_verification_tokens_user_id
on auth_email_verification_tokens (user_id);

create index if not exists ix_auth_password_reset_tokens_user_id
on auth_password_reset_tokens (user_id);
