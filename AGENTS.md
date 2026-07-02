# Agents Context

This file is durable working context for Codex agents in this project.

## Durable Rules

- Keep changes scoped. Do not refactor unrelated backend, frontend, Docker, or docs code.
- Preserve onion dependencies: `Domain` must not reference Application/Infrastructure/Api; `Application` must not reference Infrastructure/Api.
- For meaningful feature, behavior, or code changes, briefly update saved context in `README.md` and/or this file. Skip this for bug fixes and trivial changes.
- Do not commit generated files, local upload storage, logs, secrets, `node_modules`, `bin`, `obj`, or build output.
- Keep secrets out of source. Configure FASHN and database credentials with environment variables or compose files.
- Before claiming completion, run the relevant backend/frontend verification commands and report any command that could not run.
- If API contracts change, update `outfit_planner_front/src/api/client.ts`, `outfit_planner_front/src/types.ts`, tests, and README.
- If garment categories or body zones change, update Domain rules/enums, API contracts, frontend selectors/types, PostgreSQL schema/storage, and tests together.
- Preserve the same-origin frontend convention: frontend code should call `/api` and `/uploads`; Vite/nginx should proxy those paths to the API.
- Never hurry: always prioritise quality over speed.
- Don't cut corners on the practical tasks, and approach them with the utmost care—even if they are very large and require multiple stages to complete. Don't try to finish everything "in 20 minutes" or rush through the work as fast as possible. Take as much time as needed
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
- Garment categories are `Top`, `Bottom`, `Dress`, `Outerwear`, `Shoes`, `Bag`, `Accessory`, and `Hat`.
- Body zones are `Torso`, `Legs`, `FullBody`, `Feet`, `Head`, `Hands`, `Accessory`, and `OuterLayer`.
- Outfit composition uses slot compatibility rules. `Dress`/`FullBody` conflicts with `Top`/`Bottom`; duplicate exclusive slots such as Bottom or Shoes are rejected unless future layering support is added.
- Uploaded files use signed object URLs. Local storage writes object variants under `storage/objects`; MinIO/S3 is selected with `ObjectStorage__Provider=S3` or `Minio`. Outside Development the `Local` provider requires `ObjectStorage__Local__SigningSecret` (compose: `OBJECT_STORAGE_SIGNING_SECRET`) or the API refuses to start, so signed URLs are never signed with the source-visible dev key. Selecting a FASHN provider without `Fashn:ApiKey`, or S3/Minio without credentials, also fails fast at startup. Object reads go through `IObjectStorage.OpenReadObject` (not local file paths) so rotation/serving work under S3/Minio too.
- Backend error mapping: user-input/domain-rule failures throw `ValidationException` (Domain, derives from `InvalidOperationException`) and map to HTTP 400; all other exceptions reach the global handler as 500 with a trace id and no leaked detail. Do not catch broad `InvalidOperationException` in handlers for validation.
- Abuse/security: rate limiters reject with 429 + `Retry-After`; `/auth/password-reset/confirm` and AI try-on are rate limited; CSRF token-hash comparison is constant-time; production CORS origins come from `Cors:AllowedOrigins` (dev localhost is allowed only in Development); responses set `X-Content-Type-Options: nosniff` and `Referrer-Policy`, with HSTS outside Development.
- Account deletion purges the user's stored binaries (garments, body photos, avatar, AI outputs) before the DB cascade. FASHN output resolution defaults to 1k (2 credits); 4k is 5 credits. The prod compose persists uploads via the `api_storage` volume and has healthchecks/restart policies.
- Body reference photos are sensitive. Do not re-enable public `/uploads/body-reference-photos/{fileName}` serving; use signed `/api/storage/signed/...` URLs.
- Image upload handling validates magic bytes, strips metadata, auto-orients, resizes/compresses, creates thumbnails/previews/cutouts, and records perceptual hashes. Garment upload responses expose variant URLs, and new wardrobe items should use the processed cutout URL as their primary image.
- Relative garment sizing foundation: garment cutouts are trimmed to their alpha bounding box during processing, and the box is measured and persisted (`garment_items.cutout_width_px`/`cutout_height_px`, exposed as `cutoutWidthPx`/`cutoutHeightPx` on the garment DTO and upload response, flowing client→`POST /api/garments` like `perceptualHash`). Aspect (height/width) is invariant to shooting distance. The measurement refreshes wherever the cutout is re-rendered (rotation, async background removal); `GarmentCutoutMeasurementBackfillWorker` backfills existing garments best-effort (cutout variant, fallback original) using column-scoped repository updates so the two backfill workers cannot overwrite each other. Frontend: `computeRelativeSize` + `CATEGORY_SIZE_TARGETS` (`src/features/outfits/relativeSize.ts`) is the single config for per-category anchor axis and canonical display size — Top/Bottom/Dress/Outerwear normalize by width, Shoes/Bag/Accessory/Hat fit a fixed box; the Builder clothes-only stack applies it, falling back to legacy clamps when a garment is unmeasured.
- The wardrobe upload queue starts background removal eagerly when garment photos are selected (client-orchestrated, concurrency-limited, aborted if the row is removed before it finishes), shows the processed transparent cutout in the row preview, and on submit only creates garments from the already-processed result. The submit button waits while photos are still processing. No backend change: it reuses `POST /api/uploads/garment-photo`, and `StoredPhotoUrlRefresher` re-signs cutout URLs on read so a long edit gap is safe.
- Garment background removal is provider-backed during upload: default `BackgroundRemoval__Provider=Auto` uses local `rembg` when the executable is available and otherwise falls back to `Simple`; local interactive development should prefer explicit `RembgServer` with a long-running `rembg s` endpoint at `BackgroundRemoval__RembgServer__Endpoint` and model from `BackgroundRemoval__RembgServer__ModelName`, usually started with `python tools/rembg_server.py`. `Rembg` remains the slower one-CLI-process-per-upload adapter. Production can use `Http`/`CloudflareImages`-style endpoints that return transparent image bytes. Garment extraction currently assumes one item per upload via `SingleGarmentExtractionProvider`; multi-item detection/separation is only scaffolded, not active.
- Frontend is React + TypeScript + Vite under `outfit_planner_front/`.
- Frontend state/data uses TanStack Query; routing uses React Router.
- Main UI surfaces are Wardrobe, Builder, Calendar, and shared outfit view.
- Frontend visual system is editorial fashion/product UI: use `design_references/light_theme` Crimson Plinth as the canonical light palette/typography source, use `design_references/dark_theme` as dark orientation, and keep pink primary actions pink in both themes. Expected traits: warm paper/dark ink themes, Instrument Serif display headings, Inter Tight UI/body text, italic crimson emphasis, hairline borders, flat panels, compact controls, and tactile crimson primary buttons.
- Wardrobe uses category tabs as the only category filter, a compact search/control row, and a writable tag combobox backed by existing user tags. Mobile Wardrobe should show Add garment before the catalog, while the authenticated shell account/theme block sits below page content. Upload-queue tags are edited as interactive chips via `TagChipsEditor` (click a chip to rename inline, hover/focus reveals a `Trash2` delete, add through the trailing input with existing-tag suggestions); the queue has no separate comma-separated Tags field, and any chip change sets `tagsEdited` to freeze auto-suggestion.
- Wardrobe garment cards show the garment photo only, with no name, category, or tag text. Edit and Delete live in an overlay that stays in the accessibility tree (hidden via opacity, not `display`) and is revealed only on hover (desktop), keyboard focus, or press-and-hold (touch), with a document-level tap-away to dismiss; functional status badges (background removing/failed, needs-better-photo) still overlay the photo. The like/favorite and duplicate/copy card actions and the Wardrobe `Favorites` filter were removed, along with the frontend favorite/duplicate mutation code (`GarmentCard` no longer takes `onFavorite`/`onDuplicate`); the shared `is_favorite` model/API field stays (used by outfits) and the tag system (filters, editor, upload chips) is unchanged.
- Garment edit exposes a rotate/deskew control: press-and-hold or arrow-key the `RotateControl`, and the editor preview rotates the cutout by the delta from the saved angle and scales it (`fitScaleForRotation`) so the whole photo stays inside a fixed viewfinder frame. On save the absolute angle is sent to the backend, which re-renders the cutout from its immutable base.
- Builder should show body references and try-on generation controls before outfit name/save controls. Calendar mobile should show Plan day before the calendar grid, and selected current-day numbers must remain legible in light theme.
- Do not reintroduce the old claymorphism language: no Nunito display overrides, lavender canvas, animated blobs, large rounded clay panels, recessed inputs, convex purple gradients, or multi-layer neumorphic shadows. Wardrobe, Builder, Calendar, Auth, Share, and shared UI should stay aligned to the editorial system.
- Frontend app composition is split across `src/app`, route pages under `src/routes`, feature components under `src/features`, and reusable UI under `src/shared/ui`; `src/App.tsx` is only a compatibility export.
- Frontend generated OpenAPI artifacts live under ignored paths and should be regenerated with `npm run generate:api`, not committed. `pretest`/`prebuild` regenerate the client from backend OpenAPI, so frontend verification can race a concurrent backend build (see the verification skill).
- Authentication uses backend-issued `outfit_session` HttpOnly cookies plus `outfit_csrf` CSRF cookies. Frontend calls `/api` with credentials and sends `X-CSRF-Token` for mutating authenticated requests.
- Email/password auth works locally with email verification/password reset token storage, login/registration rate limiting, session list/revoke-all, and expired session cleanup support. Google OAuth and Apple OIDC are enabled only when their `Authentication__Google__*` / `Authentication__Apple__*` settings are configured.
- Account profiles persist `username`, optional signed avatar URL/object key, and `gender` (`Male`/`Female`). The shell account card opens account settings; sign out lives there behind confirmation. Avatar change/preview is only available inside account settings.
- Privacy endpoints include `DELETE /api/account`, `GET /api/account/export`, `DELETE /api/body-reference-photos/{id}`, `DELETE /api/try-on-jobs/{id}/output`, and `POST /api/privacy/purge-ai-outputs`.
- Try-on defaults to `MockTryOnProvider`. FASHN is opt-in with `TryOn__Provider=Fashn` and `Fashn__ApiKey`; the API FASHN default is `tryon-max` + `quality` (output resolution from `FASHN_RESOLUTION`, default `1k`). No gender prompt is sent by default — `tryon-max` preserves the person's identity and gender from the body photo; `Fashn__GenderPromptTemplate` is opt-in (empty by default, for garment-styling experiments only, not for dictating gender). The garment image sent to FASHN is the full-resolution processed cutout, not the thumbnail. Composite and other providers stay behind explicit provider configuration.
- Try-on generation is backend-estimated and backend-confirmed. Modes are `ClothesOnlyPreview` (free, no body reference required), `SingleGarmentTryOn` (one provider run), `SequentialOutfitTryOn` (one provider run per body garment), and `ExperimentalCompositeTryOn` (one premium composite run). Provider capabilities define credits per run; FASHN `tryon-max` quality credits follow the output resolution (`1k` = 2 credits, `4k` = 5).
- AI try-on modes are unavailable until the current account has gender set, regardless of role. Clothes-only preview remains available.
- Try-on AI input classification treats `Top`, `Bottom`, `Dress`, and `Outerwear` as body try-on items. `Shoes`, `Bag`, `Accessory`, and `Hat` are visual-only and must not be sent to AI unless the user explicitly confirms `ExperimentalCompositeTryOn`.
- Try-on providers use Application `TryOnProviderRequest` with explicit `TryOnMode`, body try-on items, visual-only items, and provider generation settings.
- Successful external try-on provider outputs are copied into app-owned object storage under `try-on-output` and exposed with signed URLs; do not persist raw FASHN/provider output URLs as user-facing previews.
- Try-on jobs cache by body reference, included garment IDs, provider, mode, and provider settings. Cache hits must not enqueue provider work or call AI.

## Common Commands

Run backend with in-memory storage over the default HTTPS launch profile:

```powershell
dotnet run --project outfit_planner_back\src\OutfitPlanner.Api\OutfitPlanner.Api.csproj
```

Run backend for local Google/Apple OAuth:

```powershell
$env:Authentication__PublicOrigin = "https://localhost:5173"
dotnet run --project outfit_planner_back\src\OutfitPlanner.Api\OutfitPlanner.Api.csproj
```

Run backend against compose PostgreSQL:

```powershell
docker compose -f docker-compose.dev.yml up -d postgres minio
$env:ConnectionStrings__Postgres = "Host=localhost;Port=5433;Database=outfit_planner;Username=outfit;Password=outfit;GSS Encryption Mode=Disable"
dotnet run --project outfit_planner_back\src\OutfitPlanner.Api\OutfitPlanner.Api.csproj
```

Run backend tests:

```powershell
dotnet run --project outfit_planner_back\tests\OutfitPlanner.Api.Tests\OutfitPlanner.Api.Tests.csproj
```

Build backend:

```powershell
dotnet build outfit_planner_back\src\OutfitPlanner.Api\OutfitPlanner.Api.csproj
```

Install frontend dependencies:

```powershell
cd outfit_planner_front
npm ci
```

Run frontend dev server over HTTPS:

```powershell
cd outfit_planner_front
npm run dev
```

Run frontend dev server over HTTPS for local Google/Apple OAuth with an explicit API target:

```powershell
cd outfit_planner_front
$env:VITE_DEV_API_TARGET = "https://localhost:5001"
npm run dev:https
```

Stop any existing Vite process on port 5173 before `dev:https`; first run requires approving the Windows `mkcert` certificate prompt.

Run frontend tests:

```powershell
cd outfit_planner_front
npm test
```

Build frontend:

```powershell
cd outfit_planner_front
npm run build
```

Run full dev stack:

```powershell
New-Item -ItemType Directory -Force .aspnet\https
dotnet dev-certs https --trust
dotnet dev-certs https -ep .aspnet\https\outfit-planner-dev.pfx -p outfit-dev-cert
docker compose -f docker-compose.dev.yml up --build
```

Run production-style stack:

```powershell
# Requires .secrets\tls\fullchain.pem and .secrets\tls\privkey.pem.
docker compose up --build
```

## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

Rules:
- For codebase questions, first run `graphify query "<question>"` when graphify-out/graph.json exists. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- After modifying code, run `graphify update .` to keep the graph current (AST-only, no API cost).
