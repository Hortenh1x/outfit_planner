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
- Garment categories are `Top`, `Bottom`, `Dress`, `Outerwear`, `Shoes`, `Bag`, `Accessory`, and `Hat`.
- Body zones are `Torso`, `Legs`, `FullBody`, `Feet`, `Head`, `Hands`, `Accessory`, and `OuterLayer`.
- Outfit composition uses slot compatibility rules. `Dress`/`FullBody` conflicts with `Top`/`Bottom`; duplicate exclusive slots such as Bottom or Shoes are rejected unless future layering support is added.
- Uploaded files use signed object URLs. Local storage writes object variants under `storage/objects`; MinIO/S3 is selected with `ObjectStorage__Provider=S3` or `Minio`.
- Body reference photos are sensitive. Do not re-enable public `/uploads/body-reference-photos/{fileName}` serving; use signed `/api/storage/signed/...` URLs.
- Image upload handling validates magic bytes, strips metadata, auto-orients, resizes/compresses, creates thumbnails/previews/cutouts, and records perceptual hashes. Garment upload responses expose variant URLs, and new wardrobe items should use the processed cutout URL as their primary image.
- Garment background removal is provider-backed during upload: default `BackgroundRemoval__Provider=Simple`, local tests can use `Rembg` with `BackgroundRemoval__Rembg__ModelName=birefnet-general`, and production can use `Http`/`CloudflareImages` style endpoints that return transparent image bytes. Garment extraction currently assumes one item per upload via `SingleGarmentExtractionProvider`; multi-item detection/separation is only scaffolded, not active.
- Frontend is React + TypeScript + Vite under `outfit_planner_front/`.
- Frontend state/data uses TanStack Query; routing uses React Router.
- Main UI surfaces are Wardrobe, Builder, Calendar, and shared outfit view.
- Frontend visual system is editorial fashion/product UI: use `design_references/light_theme` Crimson Plinth as the canonical light palette/typography source, use `design_references/dark_theme` as dark orientation, and keep pink primary actions pink in both themes. Expected traits are warm paper/dark ink themes, Instrument Serif display headings, Inter Tight UI/body text, italic crimson emphasis, hairline borders, flat panels, compact controls, and tactile crimson primary buttons.
- Wardrobe uses category tabs as the only category filter, a compact search/control row, and a writable tag combobox backed by existing user tags. Mobile Wardrobe should show Add garment before the catalog, while the authenticated shell account/theme block sits below page content.
- Builder should show body references and try-on generation controls before outfit name/save controls. Calendar mobile should show Plan day before the calendar grid, and selected current-day numbers must remain legible in light theme.
- Do not reintroduce the old claymorphism language: no Nunito display overrides, lavender canvas, animated blobs, large rounded clay panels, recessed inputs, convex purple gradients, or multi-layer neumorphic shadows. Wardrobe, Builder, Calendar, Auth, Share, and shared UI should stay aligned to the editorial system.
- Frontend app composition is split across `src/app`, route pages under `src/routes`, feature components under `src/features`, and reusable UI under `src/shared/ui`; `src/App.tsx` is only a compatibility export.
- Frontend generated OpenAPI artifacts live under ignored paths and should be regenerated with `npm run generate:api`, not committed.
- Authentication uses backend-issued `outfit_session` HttpOnly cookies plus `outfit_csrf` CSRF cookies. Frontend calls `/api` with credentials and sends `X-CSRF-Token` for mutating authenticated requests.
- Email/password auth works locally with email verification/password reset token storage, login/registration rate limiting, session list/revoke-all, and expired session cleanup support. Google OAuth and Apple OIDC are enabled only when `Authentication__Google__ClientId`/`Authentication__Google__ClientSecret` or `Authentication__Apple__ClientId`/`Authentication__Apple__ClientSecret` are configured.
- Privacy endpoints include `DELETE /api/account`, `GET /api/account/export`, `DELETE /api/body-reference-photos/{id}`, `DELETE /api/try-on-jobs/{id}/output`, and `POST /api/privacy/purge-ai-outputs`.
- Try-on defaults to `MockTryOnProvider`. FASHN is opt-in with `TryOn__Provider=Fashn` and `Fashn__ApiKey`; composite and future providers stay behind explicit provider configuration.
- Try-on generation is backend-estimated and backend-confirmed. Modes are `ClothesOnlyPreview` (free, no body reference required), `SingleGarmentTryOn` (1 credit), `SequentialOutfitTryOn` (N body garments = N credits), and `ExperimentalCompositeTryOn` (1 premium composite credit).
- Try-on AI input classification treats `Top`, `Bottom`, `Dress`, and `Outerwear` as body try-on items. `Shoes`, `Bag`, `Accessory`, and `Hat` are visual-only and must not be sent to AI unless the user explicitly confirms `ExperimentalCompositeTryOn`.
- Try-on providers use Application `TryOnProviderRequest` with explicit `TryOnMode`, body try-on items, visual-only items, and provider generation settings.
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
