create table if not exists users (
    id text primary key,
    email text,
    normalized_email text,
    display_name text,
    password_hash text,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    last_login_at timestamptz,
    email_verified_at timestamptz,
    two_factor_enabled boolean not null default false,
    avatar_url text,
    avatar_object_key text,
    gender text check (gender is null or gender in ('Male', 'Female')),
    role text not null default 'Free' check (role in ('Free', 'Premium', 'Admin'))
);

alter table users add column if not exists email text;
alter table users add column if not exists normalized_email text;
alter table users add column if not exists password_hash text;
alter table users add column if not exists updated_at timestamptz not null default now();
alter table users add column if not exists last_login_at timestamptz;
alter table users add column if not exists email_verified_at timestamptz;
alter table users add column if not exists two_factor_enabled boolean not null default false;
alter table users add column if not exists avatar_url text;
alter table users add column if not exists avatar_object_key text;
alter table users add column if not exists gender text;
alter table users add column if not exists role text not null default 'Free';

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

create unique index if not exists ux_users_normalized_email
on users (normalized_email)
where normalized_email is not null;

create table if not exists auth_external_logins (
    provider text not null,
    provider_subject text not null,
    user_id text not null references users(id) on delete cascade,
    email text,
    created_at timestamptz not null default now(),
    last_login_at timestamptz not null default now(),
    primary key (provider, provider_subject),
    unique (provider, provider_subject)
);

create index if not exists ix_auth_external_logins_user_id
on auth_external_logins (user_id);

create table if not exists auth_sessions (
    id uuid primary key,
    user_id text not null references users(id) on delete cascade,
    token_hash text not null unique,
    csrf_token_hash text not null,
    expires_at timestamptz not null,
    created_at timestamptz not null default now(),
    revoked_at timestamptz
);

create index if not exists ix_auth_sessions_user_id
on auth_sessions (user_id);

create index if not exists ix_auth_sessions_active_token_hash
on auth_sessions (token_hash)
where revoked_at is null;

create table if not exists auth_email_verification_tokens (
    token_hash text primary key,
    user_id text not null references users(id) on delete cascade,
    expires_at timestamptz not null,
    created_at timestamptz not null default now(),
    used_at timestamptz
);

create table if not exists auth_password_reset_tokens (
    token_hash text primary key,
    user_id text not null references users(id) on delete cascade,
    expires_at timestamptz not null,
    created_at timestamptz not null default now(),
    used_at timestamptz
);

create table if not exists body_reference_photos (
    id uuid primary key,
    user_id text not null references users(id) on delete cascade,
    image_url text not null,
    object_key text,
    thumbnail_object_key text,
    private_preview_object_key text,
    perceptual_hash text,
    created_at timestamptz not null default now()
);

alter table body_reference_photos add column if not exists object_key text;
alter table body_reference_photos add column if not exists thumbnail_object_key text;
alter table body_reference_photos add column if not exists private_preview_object_key text;
alter table body_reference_photos add column if not exists perceptual_hash text;

create table if not exists garment_items (
    id uuid primary key,
    user_id text not null references users(id) on delete cascade,
    name text not null,
    category text not null check (category in ('Top', 'Bottom', 'Dress', 'Outerwear', 'Shoes', 'Bag', 'Accessory')),
    body_zone text not null check (body_zone in ('Torso', 'Legs', 'FullBody', 'Feet', 'Head', 'Hands', 'Accessory', 'OuterLayer')),
    image_url text not null,
    thumbnail_url text not null,
    object_key text,
    thumbnail_object_key text,
    processed_cutout_object_key text,
    segmentation_mask_object_key text,
    perceptual_hash text,
    tags text[] not null default '{}',
    primary_color text,
    secondary_colors text[] not null default '{}',
    material text,
    brand text,
    size text,
    season text[] not null default '{}',
    weather_min_temp integer,
    weather_max_temp integer,
    occasion text[] not null default '{}',
    formality_score integer check (formality_score is null or formality_score between 1 and 5),
    warmth_score integer check (warmth_score is null or warmth_score between 1 and 5),
    comfort_score integer check (comfort_score is null or comfort_score between 1 and 5),
    is_favorite boolean not null default false,
    is_archived boolean not null default false,
    last_worn_at timestamptz,
    laundry_status text not null default 'clean' check (laundry_status in ('clean', 'worn', 'washing')),
    rotation_degrees double precision not null default 0,
    background_removal_status text not null default 'Succeeded',
    background_removal_error text,
    cutout_width_px integer,
    cutout_height_px integer,
    created_at timestamptz not null default now()
);

alter table garment_items add column if not exists primary_color text;
alter table garment_items add column if not exists object_key text;
alter table garment_items add column if not exists thumbnail_object_key text;
alter table garment_items add column if not exists processed_cutout_object_key text;
alter table garment_items add column if not exists segmentation_mask_object_key text;
alter table garment_items add column if not exists perceptual_hash text;
alter table garment_items add column if not exists secondary_colors text[] not null default '{}';
alter table garment_items add column if not exists material text;
alter table garment_items add column if not exists brand text;
alter table garment_items add column if not exists size text;
alter table garment_items add column if not exists season text[] not null default '{}';
alter table garment_items add column if not exists weather_min_temp integer;
alter table garment_items add column if not exists weather_max_temp integer;
alter table garment_items add column if not exists occasion text[] not null default '{}';
alter table garment_items add column if not exists formality_score integer;
alter table garment_items add column if not exists warmth_score integer;
alter table garment_items add column if not exists comfort_score integer;
alter table garment_items add column if not exists is_favorite boolean not null default false;
alter table garment_items add column if not exists is_archived boolean not null default false;
alter table garment_items add column if not exists last_worn_at timestamptz;
alter table garment_items add column if not exists laundry_status text not null default 'clean';
alter table garment_items add column if not exists rotation_degrees double precision not null default 0;
alter table garment_items add column if not exists background_removal_status text not null default 'Succeeded';
alter table garment_items add column if not exists background_removal_error text;
alter table garment_items add column if not exists cutout_width_px integer;
alter table garment_items add column if not exists cutout_height_px integer;

create table if not exists outfits (
    id uuid primary key,
    user_id text not null references users(id) on delete cascade,
    name text not null,
    tags text[] not null default '{}',
    occasion text[] not null default '{}',
    is_favorite boolean not null default false,
    is_archived boolean not null default false,
    clothes_only_preview_url text,
    person_preview_url text,
    hairstyle_preset_id text,
    hairstyle_visible boolean not null default true,
    silhouette_gender text check (silhouette_gender is null or silhouette_gender in ('Male', 'Female')),
    created_at timestamptz not null default now()
);

alter table outfits add column if not exists tags text[] not null default '{}';
alter table outfits add column if not exists occasion text[] not null default '{}';
alter table outfits add column if not exists is_favorite boolean not null default false;
alter table outfits add column if not exists is_archived boolean not null default false;
alter table outfits add column if not exists hairstyle_preset_id text;
alter table outfits add column if not exists hairstyle_visible boolean not null default true;
alter table outfits add column if not exists silhouette_gender text check (silhouette_gender is null or silhouette_gender in ('Male', 'Female'));

create table if not exists outfit_items (
    outfit_id uuid not null references outfits(id) on delete cascade,
    garment_id uuid not null references garment_items(id) on delete cascade,
    category text not null check (category in ('Top', 'Bottom', 'Dress', 'Outerwear', 'Shoes', 'Bag', 'Accessory')),
    primary key (outfit_id, garment_id)
);

create table if not exists scheduled_outfits (
    id uuid primary key,
    user_id text not null references users(id) on delete cascade,
    date date not null,
    outfit_id uuid not null references outfits(id) on delete cascade,
    created_at timestamptz not null default now(),
    unique (user_id, date)
);

create table if not exists try_on_jobs (
    id uuid primary key,
    user_id text not null references users(id) on delete cascade,
    outfit_id uuid not null references outfits(id) on delete cascade,
    body_reference_photo_url text not null,
    sequential_flow_enabled boolean not null default false,
    status text not null,
    provider_job_id text,
    provider_name text,
    provider_request_id text,
    source_body_photo_id uuid references body_reference_photos(id) on delete set null,
    output_image_url text,
    error text,
    consent_accepted_at timestamptz,
    retention_until timestamptz,
    is_deleted boolean not null default false,
    try_on_mode text not null default 'SequentialOutfitTryOn',
    confirmed_credits integer not null default 0,
    cache_key text,
    served_from_cache boolean not null default false,
    source_cached_job_id uuid references try_on_jobs(id) on delete set null,
    provider_settings_hash text,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

alter table try_on_jobs add column if not exists sequential_flow_enabled boolean not null default false;
alter table try_on_jobs add column if not exists consent_accepted_at timestamptz;
alter table try_on_jobs add column if not exists provider_name text;
alter table try_on_jobs add column if not exists provider_request_id text;
alter table try_on_jobs add column if not exists source_body_photo_id uuid references body_reference_photos(id) on delete set null;
alter table try_on_jobs add column if not exists retention_until timestamptz;
alter table try_on_jobs add column if not exists is_deleted boolean not null default false;
alter table try_on_jobs add column if not exists try_on_mode text not null default 'SequentialOutfitTryOn';
alter table try_on_jobs add column if not exists confirmed_credits integer not null default 0;
alter table try_on_jobs add column if not exists cache_key text;
alter table try_on_jobs add column if not exists served_from_cache boolean not null default false;
alter table try_on_jobs add column if not exists source_cached_job_id uuid references try_on_jobs(id) on delete set null;
alter table try_on_jobs add column if not exists provider_settings_hash text;

create table if not exists share_links (
    token text primary key,
    user_id text not null references users(id) on delete cascade,
    outfit_id uuid not null references outfits(id) on delete cascade,
    created_at timestamptz not null default now(),
    revoked_at timestamptz
);

create index if not exists ix_garment_items_user_category
on garment_items (user_id, category);

create index if not exists ix_garment_items_user_created_at
on garment_items (user_id, created_at desc);

create index if not exists ix_scheduled_outfits_user_date
on scheduled_outfits (user_id, date);

create index if not exists ix_outfits_user_created_at
on outfits (user_id, created_at desc);

create index if not exists ix_try_on_jobs_user_cache_succeeded
on try_on_jobs (user_id, cache_key, created_at desc)
where status = 'Succeeded' and output_image_url is not null and is_deleted = false;

create index if not exists ix_garment_items_tags_gin
on garment_items using gin (tags);

create index if not exists ix_garment_items_secondary_colors_gin
on garment_items using gin (secondary_colors);

create index if not exists ix_garment_items_season_gin
on garment_items using gin (season);

create index if not exists ix_garment_items_occasion_gin
on garment_items using gin (occasion);

create index if not exists ix_outfits_tags_gin
on outfits using gin (tags);

create index if not exists ix_outfits_occasion_gin
on outfits using gin (occasion);

create index if not exists ix_try_on_jobs_user_created_at
on try_on_jobs (user_id, created_at desc);

create index if not exists ix_auth_sessions_expires_at
on auth_sessions (expires_at);

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

create table if not exists account_credit_ledger (
    id uuid primary key,
    user_id text not null references users(id) on delete cascade,
    delta integer not null,
    reason text not null check (reason in ('TrialGrant', 'SubscriptionGrant', 'TopUp', 'TryOnSpend', 'Refund', 'AdminAdjustment')),
    try_on_job_id uuid,
    expires_at timestamptz,
    created_at timestamptz not null default now()
);

create index if not exists ix_account_credit_ledger_user_id
on account_credit_ledger (user_id);

create index if not exists ix_account_credit_ledger_job
on account_credit_ledger (try_on_job_id) where try_on_job_id is not null;
