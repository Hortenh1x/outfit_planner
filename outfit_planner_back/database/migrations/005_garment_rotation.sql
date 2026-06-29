-- Persisted garment rotation (degrees). Applied on top of an immutable base cutout:
-- auto-straighten sets the initial value for clothing categories at create time, and the
-- manual rotate control edits it; the displayed cutout/thumbnail/mask are re-rendered from
-- the base and overwritten in place.
-- See docs/superpowers/specs/2026-06-29-garment-deskew-rotate-design.md
alter table garment_items add column if not exists rotation_degrees double precision not null default 0;
