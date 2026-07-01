-- Async background removal: a garment can be added before its cutout is ready, then a hosted
-- worker finishes removal and swaps in the cutout. Existing garments already have their cutout,
-- so they default to Succeeded.
alter table garment_items add column if not exists background_removal_status text not null default 'Succeeded';
alter table garment_items add column if not exists background_removal_error text;
