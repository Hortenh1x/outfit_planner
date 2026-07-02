-- The Hat garment category is retired: head wear is replaced by global hairstyle presets,
-- which are not user garments. Existing Hat garments are deleted outright (confirmed: the data
-- is not needed); outfit_items rows referencing them are removed by the ON DELETE CASCADE on
-- outfit_items.garment_id. The category CHECK constraints are then rebuilt without 'Hat'.
delete from garment_items where category = 'Hat';

alter table garment_items drop constraint if exists garment_items_category_check;
alter table garment_items add constraint garment_items_category_check check (category in ('Top', 'Bottom', 'Dress', 'Outerwear', 'Shoes', 'Bag', 'Accessory'));

alter table outfit_items drop constraint if exists outfit_items_category_check;
alter table outfit_items add constraint outfit_items_category_check check (category in ('Top', 'Bottom', 'Dress', 'Outerwear', 'Shoes', 'Bag', 'Accessory'));
