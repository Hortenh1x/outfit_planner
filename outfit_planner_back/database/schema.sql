create table if not exists users (
    id text primary key,
    display_name text,
    created_at timestamptz not null default now()
);

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
    created_at timestamptz not null default now()
);

create table if not exists outfits (
    id uuid primary key,
    user_id text not null references users(id) on delete cascade,
    name text not null,
    clothes_only_preview_url text,
    person_preview_url text,
    created_at timestamptz not null default now()
);

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
    status text not null,
    provider_job_id text,
    output_image_url text,
    error text,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create table if not exists share_links (
    token text primary key,
    user_id text not null references users(id) on delete cascade,
    outfit_id uuid not null references outfits(id) on delete cascade,
    created_at timestamptz not null default now(),
    revoked_at timestamptz
);
