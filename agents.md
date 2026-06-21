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
- Image upload handling validates magic bytes, strips metadata, auto-orients, resizes/compresses, creates thumbnails/previews/cutouts, and records perceptual hashes.
- Frontend is React + TypeScript + Vite under `outfit_planner_front/`.
- Frontend state/data uses TanStack Query; routing uses React Router.
- Main UI surfaces are Wardrobe, Builder, Calendar, and shared outfit view.
- Legacy frontend visual system is High-Fidelity Claymorphism in `src/styles.css`: Nunito headings, DM Sans body, lavender canvas, animated blobs, large rounded clay panels, recessed inputs, convex gradient buttons, and 4-layer shadows.
- New UX redesign slices should migrate visible surfaces toward the user's editorial fashion references instead of extending claymorphism: Obra Studio dark, Crimson Plinth light, warm paper/dark ink themes, serif display headings, italic crimson emphasis, hairline borders, flat panels, restrained shadows, and tactile crimson primary buttons.
- Frontend app composition is split across `src/app`, route pages under `src/routes`, feature components under `src/features`, and reusable UI under `src/shared/ui`; `src/App.tsx` is only a compatibility export.
- Frontend generated OpenAPI artifacts live under ignored paths and should be regenerated with `npm run generate:api`, not committed.
- Authentication uses backend-issued `outfit_session` HttpOnly cookies plus `outfit_csrf` CSRF cookies. Frontend calls `/api` with credentials and sends `X-CSRF-Token` for mutating authenticated requests.
- Email/password auth works locally with email verification/password reset token storage, login/registration rate limiting, session list/revoke-all, and expired session cleanup support. Google OAuth and Apple OIDC are enabled only when `Authentication__Google__ClientId`/`Authentication__Google__ClientSecret` or `Authentication__Apple__ClientId`/`Authentication__Apple__ClientSecret` are configured.
- Privacy endpoints include `DELETE /api/account`, `GET /api/account/export`, `DELETE /api/body-reference-photos/{id}`, `DELETE /api/try-on-jobs/{id}/output`, and `POST /api/privacy/purge-ai-outputs`.
- Try-on defaults to `MockTryOnProvider`. FASHN is opt-in with `TryOn__Provider=Fashn` and `Fashn__ApiKey`.
- Try-on cost estimates use Domain `TryOnMode` and Application `TryOnCostEstimator`; body try-on categories are top, bottom, dress, and outerwear, while shoes, bags, accessories, and hats are visual-only outside composite mode.
- Try-on providers use Application `TryOnProviderRequest` with explicit `TryOnMode`, body try-on items, visual-only items, and provider generation settings; the legacy outfit/options overload is temporary compatibility for `TryOnService`.
- Multi-garment FASHN generation needs the Builder page `Sequential flow` toggle.

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
