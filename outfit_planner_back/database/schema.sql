create table if not exists users (
    id text primary key,
    email text,
    normalized_email text,
    display_name text,
    password_hash text,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    last_login_at timestamptz
);

alter table users add column if not exists email text;
alter table users add column if not exists normalized_email text;
alter table users add column if not exists password_hash text;
alter table users add column if not exists updated_at timestamptz not null default now();
alter table users add column if not exists last_login_at timestamptz;

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

create table if not exists body_reference_photos (
    id uuid primary key,
    user_id text not null references users(id) on delete cascade,
    image_url text not null,
    created_at timestamptz not null default now()
);

create table if not exists garment_items (
    id uuid primary key,
    user_id text not null references users(id) on delete cascade,
    name text not null,
    category text not null check (category in ('Top', 'Bottom')),
    body_zone text not null check (body_zone in ('Torso', 'Legs')),
    image_url text not null,
    thumbnail_url text not null,
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
    created_at timestamptz not null default now()
);

alter table garment_items add column if not exists primary_color text;
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
    created_at timestamptz not null default now()
);

alter table outfits add column if not exists tags text[] not null default '{}';
alter table outfits add column if not exists occasion text[] not null default '{}';
alter table outfits add column if not exists is_favorite boolean not null default false;
alter table outfits add column if not exists is_archived boolean not null default false;

create table if not exists outfit_items (
    outfit_id uuid not null references outfits(id) on delete cascade,
    garment_id uuid not null references garment_items(id) on delete cascade,
    category text not null check (category in ('Top', 'Bottom')),
    primary key (outfit_id, category)
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
    output_image_url text,
    error text,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

alter table try_on_jobs add column if not exists sequential_flow_enabled boolean not null default false;

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
