# Hairstyle preset assets

Global hairstyle presets served by `GET /api/hairstyles` (catalog, filtered by the account's
gender) and `GET /api/hairstyles/assets/{fileName}` (SVG files). `manifest.json` is the single
source of truth mapping preset id → gender → asset file → name/sort order.

## Provenance and license

- Source: **Open Peeps** by Pablo Stanley — https://www.openpeeps.com/
- License: **CC0 1.0** (public domain dedication) — https://creativecommons.org/publicdomain/zero/1.0/
  (see `LICENSE.upstream.txt`, copied verbatim from the vendored package; the design license
  applies to these SVGs — the package's MIT code license does not apply because no code is
  vendored here)
- Vendored via the `@dicebear/open-peeps@9.4.2` npm package (`lib/components/head.js`), whose
  `meta.license` also declares CC0 1.0.
- Modifications: skin-colored paths were removed so only the single-color (#000) hair and head
  line-art remain, and each variant was wrapped as a standalone `<svg>` with a computed
  viewBox. Hat/turban/hijab head variants, afro variants, and variants that pull accent or
  clothing colors were intentionally not included.

## Regenerating / extending

The set is a curated 10 male + 10 female selection. To add or change presets, add the SVG file
here and register it in `manifest.json` (unique `id`, `gender` of `Male`/`Female`, `file`,
display `name`, `sortOrder` within the gender). The backend validates the manifest at startup
(in tests) and serves only files listed in it.
