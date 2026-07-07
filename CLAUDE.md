# Claude Code Context

Durable working context for Claude Code in the Outfit Planner project.

> **Companion file:** [`AGENTS.md`](AGENTS.md) is the Codex equivalent. This project is worked on with **both** Codex and Claude Code, so keep the **Durable Rules** and **Project Context** sections here in sync with `AGENTS.md`. When a meaningful rule or project fact changes, update `README.md` plus *both* agent context files.

## Durable Rules

- Keep changes scoped. Do not refactor unrelated backend, frontend, Docker, or docs code.
- Preserve onion dependencies: `Domain` must not reference Application/Infrastructure/Api; `Application` must not reference Infrastructure/Api.
- For meaningful feature, behavior, or code changes, briefly update saved context in `README.md` and/or this file (and `AGENTS.md`). Skip this for bug fixes and trivial changes.
- Do not commit generated files, local upload storage, logs, secrets, `node_modules`, `bin`, `obj`, or build output.
- Keep secrets out of source. Configure FASHN and database credentials with environment variables or compose files. Only refer to `.env` variable names, never their values.
- Before claiming completion, run the relevant backend/frontend verification commands and report any command that could not run. Use the `verification-before-completion` and `outfit-planner-sequential-verification` skills.
- If API contracts change, update `outfit_planner_front/src/api/client.ts`, `outfit_planner_front/src/types.ts`, tests, and README.
- If garment categories or body zones change, update Domain rules/enums, API contracts, frontend selectors/types, PostgreSQL schema/storage, and tests together.
- Preserve the same-origin frontend convention: frontend code should call `/api` and `/uploads`; Vite/nginx should proxy those paths to the API.
- The repo may be dirty. Do not revert user or previous-session (Codex) changes unless explicitly asked.
- Never hurry: always prioritise quality over speed.
- Don't cut corners on practical tasks; approach them with the utmost care even if they are very large and need multiple stages. Don't try to finish everything "in 20 minutes" or rush. Take as much time as needed.
- Always answer in russian unless you're told to do otherwise. You can you english words that represent some terms

## Project Context

- Backend is ASP.NET Core Minimal API on .NET 10.
- Backend projects live under `outfit_planner_back/src/`:
  - `OutfitPlanner.Domain`: entities, enums, domain rules.
  - `OutfitPlanner.Application`: use-case services and abstractions.
  - `OutfitPlanner.Infrastructure`: PostgreSQL store, local photo storage, security/time helpers, mock/FASHN try-on providers.
  - `OutfitPlanner.Api`: route mapping, dependency injection, CORS, upload limits, diagnostics.
- Backend storage is selected at startup:
  - Empty `ConnectionStrings__Postgres`: in-memory store.
  - Non-empty `ConnectionStrings__Postgres`: PostgreSQL store and DbUp migrations from `database/migrations`.
- `database/schema.sql` is a readable compatibility snapshot; migrations are the startup source of truth.
- Try-on jobs are queued: empty `ConnectionStrings__Redis` uses an in-memory queue; non-empty uses the Redis list queue `outfit-planner:try-on-jobs`. A hosted background worker moves jobs `Queued` → `Processing` → `Succeeded`/`Failed`.
- Garment categories are `Top`, `Bottom`, `Dress`, `Outerwear`, `Shoes`, `Bag`, and `Accessory`. The former `Hat` category is retired: migration 009 deletes legacy Hat garments (their outfit items follow via `ON DELETE CASCADE`) and rebuilds both category CHECK constraints without `Hat`, and the local file store drops Hat records from old snapshots on load.
- Body zones are `Torso`, `Legs`, `FullBody`, `Feet`, `Head`, `Hands`, `Accessory`, and `OuterLayer`.
- Head wear is covered by global hairstyle presets, not garments: `HairstylePreset` (id, gender, name, asset file, sort order) is listed by `GET /api/hairstyles` (filtered by the account's gender; empty until gender is set) with SVG assets served anonymously from `GET /api/hairstyles/assets/{fileName}`. Presets are app-owned, single hair color, never sent to AI try-on, and live under `outfit_planner_back/assets/hairstyles` — `manifest.json` is the source of truth (only manifest-listed files are served), assets vendored from Open Peeps by Pablo Stanley (CC0 1.0) via `@dicebear/open-peeps`, ~10 male + ~10 female, no afro variants. The `Head` body zone stays (hairstyles occupy it conceptually).
- Outfit composition uses slot compatibility rules. `Dress`/`FullBody` conflicts with `Top`/`Bottom`; duplicate exclusive slots such as Bottom or Shoes are rejected unless future layering support is added.
- Uploaded files use signed object URLs. Local storage writes object variants under `storage/objects`; MinIO/S3 is selected with `ObjectStorage__Provider=S3` or `Minio`.
- Body reference photos are sensitive. Do not re-enable public `/uploads/body-reference-photos/{fileName}` serving; use signed `/api/storage/signed/...` URLs.
- Image upload handling validates magic bytes, strips metadata, auto-orients, resizes/compresses, creates thumbnails/previews/cutouts, and records perceptual hashes. Garment upload responses expose variant URLs, and new wardrobe items should use the processed cutout URL as their primary image.
- The wardrobe upload queue starts background removal eagerly when garment photos are selected (client-orchestrated, concurrency-limited, aborted if the row is removed before it finishes), shows the processed transparent cutout in the row preview, and on submit only creates garments from the already-processed result. The submit button waits while photos are still processing. No backend change: it reuses `POST /api/uploads/garment-photo`, and `StoredPhotoUrlRefresher` re-signs cutout URLs on read so a long edit gap is safe.
- Relative garment sizing foundation: garment cutouts are trimmed to the bounding box of their main connected opaque mass during processing (`ImageProcessor.OpaqueBounds` labels 8-connected opaque regions and keeps the union of those ≥12% of the largest, so scattered background grain/specks an imperfect keyer leaves in corners do not balloon the box, while the whole connected garment is always kept and no real part is cropped), and the box is measured and persisted (`garment_items.cutout_width_px`/`cutout_height_px`, exposed as `cutoutWidthPx`/`cutoutHeightPx` on the garment DTO and upload response, flowing client→`POST /api/garments` like `perceptualHash`). Aspect (height/width) is invariant to shooting distance. The measurement refreshes wherever the cutout is re-rendered (rotation, async background removal); `GarmentCutoutMeasurementBackfillWorker` backfills existing garments best-effort (cutout variant, fallback original) using column-scoped repository updates so the two backfill workers cannot overwrite each other. Cutout/background-removal PNG encodes must force an alpha-bearing colour type (`PngColorType.RgbWithAlpha`, as `SimpleBackgroundRemovalProvider` now does): a bare `PngEncoder` inherits an opaque (JPEG/no-alpha) source's colour-type hint and silently drops the transparency the keyer just wrote, leaving the cutout fully opaque so the garment measures as the whole photo frame. Frontend: `computeRelativeSize` + `CATEGORY_SIZE_TARGETS` (`src/features/outfits/relativeSize.ts`) is the single config for per-category anchor axis and canonical display size — Top/Bottom/Dress/Outerwear normalize by width, Shoes/Bag/Accessory fit a fixed box; the Builder clothes-only stack applies it, falling back to legacy clamps when a garment is unmeasured.
- Duplicate garment uploads are prevented by pre-background-removal image similarity. `POST /api/uploads/garment-photo` returns a `perceptualHash` (average hash of the normalized original, computed before cutout); garments persist it (`garment_items.perceptual_hash`) and expose it on the garment DTO, and a startup `GarmentPerceptualHashBackfillWorker` backfills existing garments from their stored `Original` variant (best-effort; skips missing originals). The upload queue flags any processed photo whose hash is a near-duplicate (Hamming ≤ 5) of an existing garment or an earlier queued item (`computeDuplicateFlags`), shows a notice, dims the row, and excludes it from submit (`isCreatableItem`).
- The Add garment panel shows no shared Type/Color/Season/Tags "upload defaults" block and no empty-queue hint (before or after selecting photos); per-item metadata is edited only inside each queue row. On Builder, clicking an already-selected slot piece clears that slot (selection toggles off).
- Garment background removal is provider-backed during upload: default `BackgroundRemoval__Provider=Auto` uses local `rembg` when the executable is available and otherwise falls back to `Simple`; local interactive development should prefer explicit `RembgServer` with a long-running `rembg s` endpoint at `BackgroundRemoval__RembgServer__Endpoint` and model from `BackgroundRemoval__RembgServer__ModelName`, usually started with `python tools/rembg_server.py`. `Rembg` remains the slower one-CLI-process-per-upload adapter. Production can use `Http`/`CloudflareImages`-style endpoints that return transparent image bytes. Garment extraction currently assumes one item per upload via `SingleGarmentExtractionProvider`; multi-item detection/separation is only scaffolded, not active.
- Garment auto-tagging prefills upload metadata (category, colors, seasons, tags) as SUGGESTIONS that never overwrite user edits, mirroring the background-removal pattern: local Python service (`tools/autotag_server.py`, FastAPI) + Application `IGarmentAutoTagger` abstraction + Infrastructure HTTP/Disabled/Auto adapters + config selection with soft degradation. `AutoTagging__Provider=Auto` (default) uses the service when its `/health` endpoint is reachable and otherwise degrades to `Disabled` (empty suggestions); `HttpServer` targets it explicitly; `AutoTagging__HttpServer__Endpoint` defaults to `http://127.0.0.1:7100/classify`. Classification is a separate, non-blocking, client-orchestrated step — `POST /api/uploads/garment-photo/classify` takes the upload-response image URL + known tags, reads a clean cutout (prefers an existing cutout, else generates one from the stored original via `IGarmentCutoutFactory` over the extraction provider), and returns `GarmentAutoTagResponse` (always 200; empty when unavailable, so uploads work unchanged). Models are local and MIT-licensed: FashionCLIP (`patrickjohncyh/fashion-clip`, base `laion/CLIP-ViT-B-32-laion2B-s34B-b79K`) zero-shot for category+tags, k-means for colors (no ML), a category heuristic for seasons; images never leave the machine (GPU auto-detected, CPU fallback). Frontend `useGarmentAutoTagging` orchestrates like eager removal (concurrency-limited, abortable, passes existing tags); `applyAutoTagSuggestions` fills only untouched fields, extending the `tagsEdited` freeze to per-field flags (`categoryEdited`/`colorEdited`/`seasonEdited`). Backend tests mock the tagger (no model required). The retired `Hat` category and hairstyle presets are never auto-tagged.
- Frontend is React + TypeScript + Vite under `outfit_planner_front/`.
- Frontend state/data uses TanStack Query; routing uses React Router.
- Main UI surfaces are Wardrobe, Builder, Calendar, and shared outfit view.
- Frontend visual system is editorial fashion/product UI: use `design_references/light_theme` Crimson Plinth as the canonical light palette/typography source, use `design_references/dark_theme` as dark orientation, and keep pink primary actions pink in both themes. Expected traits: warm paper/dark ink themes, Instrument Serif display headings, Inter Tight UI/body text, italic crimson emphasis, hairline borders, flat panels, compact controls, and tactile crimson primary buttons.
- Wardrobe uses category tabs as the only category filter, a compact search/control row, and a writable tag combobox backed by existing user tags. Mobile Wardrobe should show Add garment before the catalog, while the authenticated shell account/theme block sits below page content. Upload-queue tags are edited as interactive chips via `TagChipsEditor` (click a chip to rename inline, hover/focus reveals a `Trash2` delete, add through the trailing input with existing-tag suggestions); the queue has no separate comma-separated Tags field, and any chip change sets `tagsEdited` to freeze auto-suggestion.
- Wardrobe garment cards show the garment photo only, with no name, category, or tag text. Edit and Delete live in an overlay that stays in the accessibility tree (hidden via opacity, not `display`) and is revealed only on hover (desktop), keyboard focus, or press-and-hold (touch), with a document-level tap-away to dismiss; functional status badges (background removing/failed, needs-better-photo) still overlay the photo. The like/favorite and duplicate/copy card actions and the Wardrobe `Favorites` filter were removed, along with the frontend favorite/duplicate mutation code (`GarmentCard` no longer takes `onFavorite`/`onDuplicate`); the shared `is_favorite` model/API field stays (used by outfits) and the tag system (filters, editor, upload chips) is unchanged.
- Wardrobe quick-build (Wardrobe tab only, not the Builder pieces list): each minimized `GarmentCard` carries a top-right selection checkmark (`selected`/`onToggleSelect`) and opens the full photo enlarged on tap (`GarmentPreviewDialog`, closes on backdrop/close/Esc; the enlarge tap is suppressed right after a touch long-press so it does not fight the edit/delete reveal). Selection is at most one garment per category (`wardrobeSelection.ts` category→id map, unit-tested); a floating `WardrobeBuildBar` appears while ≥1 piece is picked (it sits above the mobile bottom nav) and hands the set to the Builder via router state (`navigate('/builder', { state: { wardrobeCompose } })`), then clears the local selection. `BuilderPage` applies it once through `composedSelectionFromCategoryMap` (a picked dress is worn while the picked top/bottom are remembered), switches to clothes mode, and clears the router state. Unpicked base Top/Bottom/Shoes still fall back to the first item of their category via `ensureComposedDefaults`; Dress/Outerwear/Bag/Accessory join only when explicitly picked.
- Garment edit exposes a rotate/deskew control: press-and-hold or arrow-key the `RotateControl`, and the editor preview rotates the cutout by the delta from the saved angle and scales it (`fitScaleForRotation`) so the whole photo stays inside a fixed viewfinder frame. On save the absolute angle is sent to the backend, which re-renders the cutout from its immutable base.
- The Builder clothes-only tab renders a composed figure: a neutral flat SVG silhouette per account gender (default Female plus a hint when gender is unset, drawn in `ComposedOutfitFigure`'s 380×720 scene) with garments fitted onto fixed body zones — top→torso, bottom→legs, shoes→feet, dress→torso+legs. The silhouette is the sizing base: clothing widths come from the phase-1 `computeRelativeSize` util (real within-category proportions preserved; `CATEGORY_SIZE_TARGETS` tuned so a tee reads oversized and covers the torso). Each garment is anchored by its TOP edge and horizontally centered on the body axis: tops/dresses hang from the shoulder line (y≈144); the bottom stacks right under the worn top's rendered hem (adaptive via `TOP_BOTTOM_OVERLAP`, so the torso is always covered with no gap and the top drapes over the waistband — falls back to a fixed waist line when no top is worn); shoes center on the feet (y≈662). `garmentDisplaySize` pins WIDTH and lets height be `auto` (pinning both would let `object-fit:contain` letterbox wide garments narrow), and `.figure-slot img` sets `max-width:none` because the global `img{max-width:100%}` would otherwise clamp any garment wider than SCENE_WIDTH−CENTER_X. Cycle arrows are absolute overlays so they never shove the garment off-center. Every garment category has a Wardrobe-pieces list entry. Top/bottom/shoes are additionally cyclable directly on the figure (touch swipe + desktop arrows, wrap-around, first item worn by default, empty categories show placeholder slots); clicking their list entry sets the worn piece (via `placeGarment`, dropping any worn dress). Dress/outerwear/bag/accessory are picked from the side list and deselected by tapping the piece on the figure; the dress also cycles, selecting it hides top+bottom and unselecting restores exactly the remembered pair (nothing else changes); accessories cap at 3 (left rail), bag at 1 (right-lower), outerwear behaves like a side piece (right-upper), all in fixed side boxes clear of the clothing. Pure selection rules live in `src/features/outfits/composedOutfit.ts` (unit-tested).
- Hairstyle presets are currently HIDDEN from the product: the Builder composes with no hairstyle, cards/share do not show one, and outfits do not save a hairstyle preset. The capability is kept intact for later — `composedOutfit.ts` hairstyle rules, `ComposedOutfitFigure`'s hairstyle rendering props, the backend `hairstyle_preset_id`/`hairstyle_visible` columns, `GET /api/hairstyles`, and the preset catalog all remain; only the visible/saved wiring is disconnected. `silhouette_gender` is still saved.
- Outfits persist the composed figure so cards and shared views reconstruct it exactly: `outfits.hairstyle_preset_id`/`hairstyle_visible`/`silhouette_gender` (migration 010; null-safe for legacy outfits), cutout measurements flow onto outfit items via the garment join, and outfit DTOs carry a response-only `hairstyleAssetUrl` resolved from the preset catalog (so the anonymous shared view needs no authenticated preset listing). `ComposedOutfitFigure` (`src/features/outfits/ComposedOutfitFigure.tsx`) is the single renderer used by the Builder canvas, saved-outfit cards, and SharePage.
- A generated try-on preview is saved onto the outfit by default: the succeeded/cached try-on job sets `outfits.person_preview_url` (`PersonPreviewUrl`), and it survives metadata-only saves — `OutfitService.UpdateOutfit` clears it only when the worn garment *set* actually changes, not merely because the Builder always re-sends `garmentIds` on rename/save. Saved-outfit cards show the generated preview when the outfit has one (else the composed figure). Clicking a saved-outfit card enlarges it in a view-only dialog (`OutfitPreviewDialog`, `src/features/builder/OutfitPreviewDialog.tsx`, mirroring the wardrobe `GarmentPreviewDialog`; closes on backdrop/X/Esc, shows the generated preview or the composed figure); its `Open in builder` action loads the outfit into the Builder for editing, regeneration, or sharing.
- Builder should show body references and try-on generation controls before outfit name/save controls. On mobile (single-column Builder, ≤1240px), the Wardrobe pieces list sits below the main Builder screen — the stack order is preview → Wardrobe pieces → Outfit controls (via CSS `order`, builder-scoped so Calendar is unaffected). Calendar mobile should show Plan day before the calendar grid, and selected current-day numbers must remain legible in light theme.
- Do not reintroduce the old claymorphism language: no Nunito display overrides, lavender canvas, animated blobs, large rounded clay panels, recessed inputs, convex purple gradients, or multi-layer neumorphic shadows. Wardrobe, Builder, Calendar, Auth, Share, and shared UI should stay aligned to the editorial system.
- Frontend app composition is split across `src/app`, route pages under `src/routes`, feature components under `src/features`, and reusable UI under `src/shared/ui`; `src/App.tsx` is only a compatibility export.
- Frontend generated OpenAPI artifacts live under ignored paths and should be regenerated with `npm run generate:api`, not committed. `pretest`/`prebuild` regenerate the client from backend OpenAPI, so frontend verification can race a concurrent backend build (see the verification skill).
- Authentication uses backend-issued `outfit_session` HttpOnly cookies plus `outfit_csrf` CSRF cookies. Frontend calls `/api` with credentials and sends `X-CSRF-Token` for mutating authenticated requests.
- Email/password auth works locally with email verification/password reset token storage, login/registration rate limiting, session list/revoke-all, and expired session cleanup support. Google OAuth and Apple OIDC are enabled only when their `Authentication__Google__*` / `Authentication__Apple__*` settings are configured.
- Account profiles persist `username`, optional signed avatar URL/object key, and `gender` (`Male`/`Female`). The shell account card opens account settings; sign out lives there behind confirmation. Avatar change/preview is only available inside account settings.
- Account roles are the paywall foundation: `UserRole` (`Free`/`Premium`/`Admin`), stored in `users.role` (migration 011, default `Free`) and exposed as `role` on the session user (`AuthUserResponse`). The effective role is pinned ?? stored via the Application `RolePinningPolicy`: `dmytro.bolibok@gmail.com` is always `Admin` and `olya.shaydur@gmail.com` always `Premium` (defaults; `Roles__PinnedAdminEmails`/`Roles__PinnedPremiumEmails` override, blank config keeps the built-in pins). Sign-in folds the effective role into the existing `UpdateUser` write so stored roles converge in all three stores (the file-backed store has no migrations). Roles gate no product features yet; `PAYWALL_MODEL.md` at the repo root is the paywall proposal.
- Admin panel: `/api/admin/*` (stats; paged/searchable/role-filtered user list with per-user data counts via `IAdminUserRepository`; user detail; `PUT .../role`; revoke sessions; purge AI outputs; sanitized export without password hash/object keys; delete account reusing the shared purge flow) is gated by the effective Admin role that the session middleware stashes in `HttpContext.Items`. `AdminService` guards: admins cannot change their own role or delete themselves via admin routes, pinned accounts can be neither re-roled nor deleted (409 Conflict). Frontend: `/admin` route (`AdminPage` + `RequireAdmin` gate redirecting non-admins to `/builder`), with an Admin nav entry rendered only for admins; the role select is disabled with a "Pinned" pill for pinned accounts.
- Privacy endpoints include `DELETE /api/account`, `GET /api/account/export`, `DELETE /api/body-reference-photos/{id}`, `DELETE /api/try-on-jobs/{id}/output`, and `POST /api/privacy/purge-ai-outputs`.
- Try-on defaults to `MockTryOnProvider`. FASHN is opt-in with `TryOn__Provider=Fashn` and `Fashn__ApiKey`; the API FASHN default is `tryon-max` + `quality` (output resolution from `FASHN_RESOLUTION`, default `1k`). No gender prompt is sent by default — `tryon-max` preserves the person's identity and gender from the body photo; `Fashn__GenderPromptTemplate` is opt-in (empty by default, for garment-styling experiments only, not for dictating gender). The garment image sent to FASHN is the full-resolution processed cutout, not the thumbnail. Composite and other providers stay behind explicit provider configuration.
- Try-on generation is backend-estimated and backend-confirmed. Modes are `ClothesOnlyPreview` (free, no body reference required), `SingleGarmentTryOn` (one provider run), `SequentialOutfitTryOn` (one provider run per body garment), and `ExperimentalCompositeTryOn` (one premium composite run). Provider capabilities define credits per run; FASHN `tryon-max` quality credits follow the output resolution (`1k` = 2 credits, `4k` = 5).
- AI try-on modes are unavailable until the current account has gender set, regardless of role. Clothes-only preview remains available.
- Try-on AI input classification treats `Top`, `Bottom`, `Dress`, and `Outerwear` as body try-on items. `Shoes`, `Bag`, and `Accessory` are visual-only and must not be sent to AI unless the user explicitly confirms `ExperimentalCompositeTryOn`. Hairstyle presets are not garments and are never part of try-on provider requests.
- Try-on providers use Application `TryOnProviderRequest` with explicit `TryOnMode`, body try-on items, visual-only items, and provider generation settings.
- Successful external try-on provider outputs are copied into app-owned object storage under `try-on-output` and exposed with signed URLs; do not persist raw FASHN/provider output URLs as user-facing previews.
- Try-on jobs cache by body reference, included garment IDs, provider, mode, and provider settings. Cache hits must not enqueue provider work or call AI.

## Skills

Personal skills live in `~/.claude/skills/` (ported from the Codex `~/.codex/skills/` setup). Most relevant here:

- **Process:** `brainstorming` (before any creative/feature work), `writing-plans`, `executing-plans`, `subagent-driven-development`, `dispatching-parallel-agents`.
- **Quality/verification:** `test-driven-development`, `systematic-debugging`, `verification-before-completion`, `requesting-code-review`, `receiving-code-review`, `karpathy-guidelines`, `finishing-a-development-branch`, `using-git-worktrees`.
- **This project specifically:** `outfit-planner-sequential-verification` — the ordered backend→frontend verification checklist that avoids the OpenAPI-generation race and stale dev shells. Use it for any backend-contract / generated-client / Builder-Calendar-AppShell / FASHN change.
- **Design (editorial UI):** `impeccable` (`/impeccable <command>`), `emil-design-eng`, `design-taste-frontend`. Verify UI visually when changing it.

## Common Commands

Current environment is **Linux** (`bash`). The original project docs and `AGENTS.md` use PowerShell because the project was first developed on Windows; the equivalents below are the Linux forms.

Run backend with in-memory storage:

```bash
dotnet run --project outfit_planner_back/src/OutfitPlanner.Api/OutfitPlanner.Api.csproj
```

Run backend for local Google/Apple OAuth:

```bash
Authentication__PublicOrigin="https://localhost:5173" \
  dotnet run --project outfit_planner_back/src/OutfitPlanner.Api/OutfitPlanner.Api.csproj
```

Run backend against compose PostgreSQL + Redis:

```bash
docker compose -f docker-compose.dev.yml up -d postgres redis minio
ConnectionStrings__Postgres="Host=localhost;Port=5433;Database=outfit_planner;Username=outfit;Password=outfit;GSS Encryption Mode=Disable" \
ConnectionStrings__Redis="localhost:6379" \
  dotnet run --project outfit_planner_back/src/OutfitPlanner.Api/OutfitPlanner.Api.csproj
```

Run backend tests (console test runner):

```bash
dotnet run --project outfit_planner_back/tests/OutfitPlanner.Api.Tests/OutfitPlanner.Api.Tests.csproj
```

Build backend:

```bash
dotnet build outfit_planner_back/src/OutfitPlanner.Api/OutfitPlanner.Api.csproj
```

Frontend (install, dev, test, build):

```bash
cd outfit_planner_front
npm ci
npm run dev        # HTTPS dev server on https://localhost:5173, proxies /api + /uploads to https://localhost:5001
npm test           # regenerates the API client first
npm run build      # regenerates the API client first
```

Target a different local API for the dev server:

```bash
cd outfit_planner_front
VITE_DEV_API_TARGET="https://localhost:5001" npm run dev
```

Regenerate the frontend API client from backend OpenAPI:

```bash
cd outfit_planner_front
npm run generate:api
```

Local `rembg` background-removal server (recommended for interactive dev):

```bash
python tools/rembg_server.py --host 127.0.0.1 --port 7000 --model birefnet-general-lite
```

Local garment auto-tagging service (optional; prefills upload metadata — see the auto-tagging Project Context bullet). Use a venv (Arch/PEP 668 blocks a global `pip install`):

```bash
python -m venv .venv-autotag && source .venv-autotag/bin/activate
pip install torch --index-url https://download.pytorch.org/whl/cu124   # match your CUDA; omit --index-url for CPU (GPU auto-detected)
pip install -r tools/requirements-autotag.txt
python tools/autotag_server.py --host 127.0.0.1 --port 7100
```

Verification order for changes that touch contracts/UI (do **not** parallelize backend build/test with the frontend `npm` steps — that overlap is the known OpenAPI-generation race): backend test → backend build → frontend test → frontend build → browser sanity-check → `git diff --check`. See the `outfit-planner-sequential-verification` skill.

## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

Rules:
- For codebase questions, first run `graphify query "<question>"` when graphify-out/graph.json exists. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- After modifying code, run `graphify update .` to keep the graph current (AST-only, no API cost).
