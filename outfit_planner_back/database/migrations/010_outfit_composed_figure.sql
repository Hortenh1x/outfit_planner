-- Composed-figure outfit state: which global hairstyle preset the outfit wears, whether it is
-- visible, and the silhouette gender the outfit was composed on. Null on legacy outfits, so
-- existing rows keep working (composed rendering falls back to defaults).
alter table outfits add column if not exists hairstyle_preset_id text;
alter table outfits add column if not exists hairstyle_visible boolean not null default true;
alter table outfits add column if not exists silhouette_gender text check (silhouette_gender is null or silhouette_gender in ('Male', 'Female'));
