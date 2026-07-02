-- Relative-size foundation: alpha-bounding-box size of the garment's processed cutout in
-- pixels. Absolute values depend on the shot, but height/width is invariant to shooting
-- distance and drives per-category relative rendering. Null means "not measured yet"; a
-- startup backfill worker measures existing garments best-effort.
alter table garment_items add column if not exists cutout_width_px integer;
alter table garment_items add column if not exists cutout_height_px integer;
