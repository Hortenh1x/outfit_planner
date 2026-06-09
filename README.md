# Outfit Planner

Outfit Planner is a research-demo web app for cataloging garments, composing outfits, planning outfits by day, sharing saved outfits, and generating AI try-on previews through a replaceable provider adapter.

The app is intentionally small, but it has a real backend/frontend split, signed object storage for uploads, optional PostgreSQL persistence, and an optional FASHN provider scaffold. By default it uses in-memory storage, local object storage, and the mock try-on provider, so local development does not need paid AI credentials.

## Features

- Wardrobe catalog for Top, Bottom, Dress, Outerwear, Shoes, Bag, Accessory, and Hat garments with editable structured metadata for colors, material, brand, size, season, weather, occasion, scoring, favorites, archive state, last worn date, and laundry status.
- Garment photo uploads from the browser.
- Private body reference photo uploads for try-on generation.
- Outfit builder with slot compatibility rules instead of one-garment-per-category rules.
- Clothes-only and generated person preview modes.
- Calendar planning with one outfit per user and day.
- Share links for saved outfits.
- Secure account registration and sign-in with email/password.
- Google OAuth and Apple OIDC sign-in when provider credentials are configured.
- Revocable server-side sessions with HttpOnly cookies, CSRF protection, rate-limited auth endpoints, email verification/password reset token storage, and session revoke-all support.
- Privacy endpoints for account export/delete, body photo deletion, and AI output purging.
- Background AI try-on jobs with a Redis-backed queue in Docker and an in-memory queue fallback for local development.
- Mock AI try-on by default, optional FASHN `tryon-v1.6`, local VTON/CatVTON, Replicate, and Fal provider adapters.

## Tech Stack

- Backend: ASP.NET Core Minimal API on .NET 10.
- Backend architecture: onion-style `Domain`, `Application`, `Infrastructure`, and `Api` projects.
- Persistence: PostgreSQL through Npgsql and DbUp migrations when `ConnectionStrings__Postgres` is configured; in-memory fallback otherwise.
- Background queue: Redis through `StackExchange.Redis` when `ConnectionStrings__Redis` is configured; in-memory fallback otherwise.
- Object storage: signed local object storage by default, or S3-compatible MinIO through `ObjectStorage__Provider=S3`/`Minio`.
- Frontend: React, TypeScript, Vite, TanStack Query, React Router, date-fns, lucide-react.
- Infra: PostgreSQL 18, MinIO placeholder service, Docker Compose, nginx for the production frontend container.

## Repository Layout

```text
.
|-- README.md
|-- agents.md
|-- .gitignore
|-- docker-compose.yml
|-- docker-compose.dev.yml
|-- outfit_planner_back/
|   |-- Dockerfile
|   |-- database/
|   |   |-- schema.sql
|   |   `-- migrations/
|   |-- src/
|   |   |-- OutfitPlanner.Domain/
|   |   |-- OutfitPlanner.Application/
|   |   |-- OutfitPlanner.Infrastructure/
|   |   `-- OutfitPlanner.Api/
|   `-- tests/OutfitPlanner.Api.Tests/
`-- outfit_planner_front/
    |-- Dockerfile
    |-- nginx.conf
    |-- package.json
    |-- vite.config.ts
    `-- src/
        |-- App.tsx
        |-- api/client.ts
        |-- components/
        |-- features/
        `-- types.ts
```

## Architecture Notes

The backend follows inward dependencies:

- `OutfitPlanner.Domain` contains entities, enums, and domain rules.
- `OutfitPlanner.Application` contains use-case services and repository/provider abstractions.
- `OutfitPlanner.Infrastructure` implements PostgreSQL storage, local/S3-compatible object storage, image processing, clocks, password hashing, auth token hashing, share token generation, and try-on providers.
- `OutfitPlanner.Api` wires dependencies, JSON/CORS/upload limits, routes, diagnostics, secure auth cookies, OAuth/OIDC callbacks, and CSRF enforcement.

The API chooses storage at startup:

- Empty `ConnectionStrings:Postgres`: use `InMemoryOutfitStore`.
- Non-empty `ConnectionStrings:Postgres`: use `PostgresOutfitStore` and apply DbUp SQL migrations from `outfit_planner_back/database/migrations`.

Photo uploads are private by default:

- Uploads are validated server-side by image magic bytes, then decoded, auto-oriented, stripped of metadata, resized/compressed, and stored as object variants.
- Garment uploads store original, thumbnail, processed cutout, optional segmentation mask, and perceptual hash.
- Body reference uploads store original, thumbnail, blurred private preview, and perceptual hash. Public `/uploads/body-reference-photos/{fileName}` access is disabled; clients receive signed object URLs.

Try-on jobs are queued at request time:

- Empty `ConnectionStrings:Redis`: use an in-memory try-on queue.
- Non-empty `ConnectionStrings:Redis`: use Redis list queue `outfit-planner:try-on-jobs`.
- `POST /api/outfits/{outfitId}/try-on` creates a `Queued` job and returns `202 Accepted`; a hosted background worker moves the job through `Processing` to `Succeeded` or `Failed`.

The frontend uses a same-origin API path by default:

- `VITE_API_URL` defaults to `/api`.
- Vite proxies `/api` and `/uploads` to `VITE_DEV_API_TARGET` or `http://localhost:5000`.
- The production Docker frontend builds with `VITE_API_URL=/api` and nginx proxies `/api/` and `/uploads/` to the API service.

Authentication is cookie-backed:

- Email/password registration and sign-in create an opaque server-side session.
- Session cookie `outfit_session` is HttpOnly, SameSite=Lax, and Secure outside development.
- CSRF cookie `outfit_csrf` is readable by the frontend and must be echoed as `X-CSRF-Token` on mutating authenticated API requests.
- Google and Apple sign-in start from backend challenge endpoints and complete through backend callbacks. If the external account is new, the API creates it automatically. If the provider returns a verified email that already exists, the external login is linked to that user.
- All private `/api` routes require a valid session. `/api/health`, `/api/system/status`, `/api/auth/*`, `/api/storage/signed/*`, and `/api/share/{token}` remain public; signed storage access is protected by URL signature and expiry.

The frontend visual system is High-Fidelity Claymorphism:

- Global tokens live in `outfit_planner_front/src/styles.css`.
- The interface uses Nunito for display text, DM Sans for body copy, lavender canvas color `#F4F1FA`, animated ambient blobs, large rounded panels, recessed inputs, convex gradient buttons, and 4-layer clay shadow stacks.
- `outfit_planner_front/src/App.tsx` keeps the existing routes and flows while surfacing service metadata endpoints in the shell and try-on job status after generation.

## Prerequisites

- .NET 10 SDK.
- Node.js 24 or a recent Node version compatible with the locked frontend dependencies.
- Docker Desktop or another Docker Compose runtime for PostgreSQL/MinIO/container workflows.

## Quick Start With Docker

Development containers with hot reload:

```powershell
docker compose -f docker-compose.dev.yml up --build
```

Open:

- Frontend: `http://localhost:5173`
- API: `http://localhost:5000/api/health`
- PostgreSQL on host: `localhost:5433`
- Redis on host: `localhost:6379`
- MinIO API: `http://localhost:9000`
- MinIO console: `http://localhost:9001`

Production-style containers:

```powershell
docker compose up --build
```

The production compose file builds the API and static frontend images. The frontend container serves the React app through nginx and proxies API/upload requests to the API container.

## Local Backend Development

Run the API with in-memory storage:

```powershell
dotnet run --project outfit_planner_back\src\OutfitPlanner.Api\OutfitPlanner.Api.csproj --urls http://localhost:5000
```

For local OAuth testing, run the API on HTTPS:

```powershell
$env:Authentication__PublicOrigin = "https://localhost:5173"
dotnet run --project outfit_planner_back\src\OutfitPlanner.Api\OutfitPlanner.Api.csproj --urls https://localhost:5001
```

Run PostgreSQL and MinIO only:

```powershell
docker compose -f docker-compose.dev.yml up -d postgres redis minio
```

Run the API against the compose PostgreSQL service:

```powershell
$env:ConnectionStrings__Postgres = "Host=localhost;Port=5433;Database=outfit_planner;Username=outfit;Password=outfit;GSS Encryption Mode=Disable"
$env:ConnectionStrings__Redis = "localhost:6379"
dotnet run --project outfit_planner_back\src\OutfitPlanner.Api\OutfitPlanner.Api.csproj --urls http://localhost:5000
```

For PostgreSQL plus local OAuth testing, use the same connection strings and run the API at `https://localhost:5001`.

The API writes uploaded files below its content root:

```text
outfit_planner_back/src/OutfitPlanner.Api/storage/
```

This folder is local runtime state and should not be committed.

## Local Frontend Development

Install dependencies:

```powershell
cd outfit_planner_front
npm ci
```

Run Vite:

```powershell
npm run dev
```

Open `http://127.0.0.1:5173`. During Vite development, the browser calls `/api` and `/uploads`; Vite proxies those requests to `http://localhost:5000` unless `VITE_DEV_API_TARGET` is set.

Run Vite over HTTPS for Google/Apple OAuth testing:

```powershell
$env:VITE_DEV_API_TARGET = "https://localhost:5001"
npm run dev:https
```

Stop any existing `npm run dev` process on port `5173` before starting the HTTPS server. On the first run, approve the Windows certificate prompt from `mkcert`; this installs a local development CA so the browser can trust `https://localhost:5173`.

Open `https://localhost:5173`. The HTTPS dev server uses a local development certificate through `vite-plugin-mkcert` and forwards the browser-facing HTTPS scheme and host to the API. In Google Cloud Console, add this exact Authorized JavaScript origin for local development:

```text
https://localhost:5173
```

Also add this exact Authorized redirect URI:

```text
https://localhost:5173/api/auth/external/google/callback
```

The provider callback path is only for Google/Apple to return to the API. After the provider callback succeeds, the API redirects internally to `/api/auth/external/{provider}/complete` to issue app cookies and then returns to the requested app route.

To target a different local API:

```powershell
$env:VITE_DEV_API_TARGET = "http://localhost:5001"
npm run dev
```

## Configuration

Backend configuration can be supplied through `appsettings.json`, environment variables, or Docker Compose.

| Setting | Default | Purpose |
| --- | --- | --- |
| `ConnectionStrings__Postgres` | empty | Enables PostgreSQL persistence when non-empty. |
| `ConnectionStrings__Redis` | empty | Enables the Redis try-on job queue when non-empty. Empty uses an in-memory queue. |
| `ObjectStorage__Provider` | `Local` | Use `Local`, `S3`, or `Minio` for uploaded image object storage. |
| `ObjectStorage__Local__Root` | `storage/objects` under API content root | Local object storage root. |
| `ObjectStorage__Local__SigningSecret` | development fallback | HMAC secret for local signed object URLs. Set a strong value outside dev. |
| `ObjectStorage__S3__Endpoint` / `Minio__Endpoint` | empty | S3-compatible endpoint for MinIO/S3 storage. |
| `ObjectStorage__S3__AccessKey` / `Minio__AccessKey` | empty | S3-compatible access key. |
| `ObjectStorage__S3__SecretKey` / `Minio__SecretKey` | empty | S3-compatible secret key. |
| `ObjectStorage__S3__Bucket` / `Minio__Bucket` | `outfit-planner-private` | Private bucket for uploaded image variants. |
| `TryOn__Provider` | `Mock` | Use `Mock`, `Fashn`, `LocalVton`, `LocalCatVton`, `Replicate`, or `Fal`. Unknown values use the mock provider. |
| `Fashn__ApiKey` | empty | Required before the FASHN provider makes network calls. |
| `Fashn__BaseUrl` | `https://api.fashn.ai/v1/` | FASHN API base URL. |
| `Fashn__ModelName` | `tryon-v1.6` | FASHN model name. |
| `Fashn__Mode` | `balanced` | FASHN generation mode. |
| `Fashn__MaxPollingAttempts` | `30` | Status polling limit. |
| `Fashn__PollIntervalSeconds` | `2` | Delay between status polls. |
| `Fashn__TimeoutSeconds` | `180` | HTTP client timeout. |
| `TryOn__LocalVton__BaseUrl` | `http://localhost:7860/` | Local/dev VTON endpoint base URL. |
| `TryOn__LocalVton__Endpoint` | `/try-on` | Local/dev VTON generation endpoint. |
| `TryOn__LocalCatVton__BaseUrl` | `http://localhost:7861/` | Local/dev CatVTON endpoint base URL. |
| `TryOn__LocalCatVton__Endpoint` | `/try-on` | Local/dev CatVTON generation endpoint. |
| `TryOn__Replicate__ApiKey` | empty | Required when `TryOn__Provider=Replicate`. |
| `TryOn__Replicate__BaseUrl` | `https://api.replicate.com/v1/` | Replicate adapter base URL. |
| `TryOn__Replicate__Endpoint` | `/predictions` | Replicate adapter endpoint. |
| `TryOn__Fal__ApiKey` | empty | Required when `TryOn__Provider=Fal`. |
| `TryOn__Fal__BaseUrl` | `https://fal.run/` | Fal adapter base URL. |
| `TryOn__Fal__Endpoint` | `/try-on` | Fal adapter endpoint. |
| `DetailedErrors` | environment-dependent | Enables structured exception details in dev/test diagnostics. |
| `Authentication__PublicOrigin` | `https://localhost:5173` | Canonical browser-facing origin used to build Google/Apple callback URLs. Set this to the exact frontend origin registered in the OAuth provider. |
| `Authentication__Google__ClientId` | empty | Enables Google OAuth when paired with `Authentication__Google__ClientSecret`. |
| `Authentication__Google__ClientSecret` | empty | Google OAuth client secret. |
| `Authentication__Apple__ClientId` | empty | Enables Apple OIDC when paired with `Authentication__Apple__ClientSecret`. |
| `Authentication__Apple__ClientSecret` | empty | Apple OIDC client secret JWT generated from Apple developer credentials. |

Frontend configuration:

| Setting | Default | Purpose |
| --- | --- | --- |
| `VITE_API_URL` | `/api` | Base URL used by `src/api/client.ts`. |
| `VITE_DEV_API_TARGET` | `http://localhost:5000` | Vite dev proxy target for `/api` and `/uploads`; use `https://localhost:5001` for local OAuth testing. |

## Optional Try-On Providers

The backend uses the mock try-on provider by default. The mock returns deterministic demo output and does not spend credits. All non-mock providers run from the hosted background worker after `POST /api/outfits/{outfitId}/try-on` has already returned a queued job.

Enable the FASHN scaffold:

```powershell
$env:TryOn__Provider = "Fashn"
$env:Fashn__ApiKey = "YOUR_FASHN_API_KEY"
dotnet run --project outfit_planner_back\src\OutfitPlanner.Api\OutfitPlanner.Api.csproj --urls http://localhost:5000
```

The FASHN provider submits to `/run` and polls `/status/{id}`. A single-garment outfit maps directly to one provider run. Multi-garment outfits require the Builder page's `Sequential flow` toggle; the provider then applies garments one after another, using the previous output image as the next model image. This can consume one provider run per garment.

Local/dev provider adapters can target a local HTTP service:

```powershell
$env:TryOn__Provider = "LocalVton"
$env:TryOn__LocalVton__BaseUrl = "http://localhost:7860/"
$env:TryOn__LocalVton__Endpoint = "/try-on"
```

`LocalCatVton`, `Replicate`, and `Fal` use the same JSON adapter shape. `Replicate` and `Fal` require `TryOn__Replicate__ApiKey` or `TryOn__Fal__ApiKey` respectively.

## API Overview

All API routes below are under `/api` unless noted. JSON enum values are serialized as strings.

Private routes require the `outfit_session` cookie. Mutating private routes also require the `X-CSRF-Token` header matching the `outfit_csrf` cookie.

| Method | Path | Purpose |
| --- | --- | --- |
| `GET` | `/health` | Basic API health check. |
| `GET` | `/system/status` | API, storage, PostgreSQL, and AI provider status. |
| `GET` | `/auth/providers` | Email, Google, and Apple auth provider metadata. |
| `POST` | `/auth/register` | Create an email/password account and issue auth cookies. |
| `POST` | `/auth/login` | Sign in with email/password and issue auth cookies. |
| `POST` | `/auth/logout` | Revoke the current session and clear auth cookies. |
| `GET` | `/auth/me` | Read the current authenticated user session. |
| `POST` | `/auth/email-verification/request` | Create an email verification token. |
| `POST` | `/auth/email-verification/confirm` | Verify an email verification token. |
| `POST` | `/auth/password-reset/request` | Create a password reset token. |
| `POST` | `/auth/password-reset/confirm` | Reset a password and revoke existing sessions. |
| `GET` | `/auth/sessions` | List active sessions. |
| `DELETE` | `/auth/sessions` | Revoke all sessions for the current user. |
| `GET` | `/auth/external/{provider}/start` | Start Google or Apple auth with `returnUrl`. |
| `GET` | `/auth/external/{provider}/callback` | OAuth/OIDC provider callback path registered with Google/Apple. |
| `GET` | `/auth/external/{provider}/complete` | Complete external auth after the provider callback and issue app cookies. |
| `GET` | `/account/export` | Export the current user's account, wardrobe, body photos, outfits, and try-on jobs. |
| `DELETE` | `/account` | Delete the current account and clear auth cookies. |
| `GET` | `/body-reference-photos` | List body reference photos for the current user. |
| `POST` | `/body-reference-photos` | Register an already uploaded body reference photo URL. |
| `DELETE` | `/body-reference-photos/{photoId}` | Delete a body reference photo and its stored file when local. |
| `GET` | `/garments?category=Top&color=black&season=summer&q=shirt&sort=recent&offset=0&limit=20` | List garments for the current user with optional filters, sorting, and pagination. |
| `GET` | `/garments/{garmentId}` | Read one garment owned by the current user. |
| `POST` | `/garments` | Create a garment. |
| `PATCH` | `/garments/{garmentId}` | Edit garment name, category, tags, and structured metadata without re-uploading the photo. |
| `DELETE` | `/garments/{garmentId}` | Delete a garment and its stored file when local. |
| `POST` | `/uploads/garment-photo` | Multipart garment photo upload. |
| `POST` | `/uploads/body-reference-photo` | Multipart body reference photo upload. |
| `GET` | `/outfits?q=office&occasion=business&favorite=true&sort=recent&offset=0&limit=20` | List saved outfits with optional filters, sorting, and pagination. |
| `GET` | `/outfits/{outfitId}` | Read one outfit owned by the current user. |
| `POST` | `/outfits` | Create an outfit. |
| `PATCH` | `/outfits/{outfitId}` | Edit outfit name, garments, tags, occasion, favorite, and archive state. |
| `DELETE` | `/outfits/{outfitId}` | Delete an outfit. |
| `POST` | `/outfits/{outfitId}/try-on` | Queue try-on generation and return `202 Accepted`. Accepts optional `bodyReferencePhotoId` for audit linkage. |
| `GET` | `/try-on-jobs/{jobId}` | Read try-on job status/result. |
| `DELETE` | `/try-on-jobs/{jobId}/output` | Mark one try-on output deleted and remove the stored output URL from the job. |
| `POST` | `/privacy/purge-ai-outputs` | Mark all current-user AI outputs deleted and remove stored output URLs from jobs. |
| `POST` | `/schedule` | Plan an outfit for a date. |
| `GET` | `/schedule?from=YYYY-MM-DD&to=YYYY-MM-DD` | List planned outfits for a date range. |
| `DELETE` | `/schedule/{date}` | Remove the planned outfit for one date. |
| `POST` | `/outfits/{outfitId}/share` | Create a share link. |
| `GET` | `/share/{token}` | Read a shared outfit. |
| `DELETE` | `/share/{token}` | Revoke a share link owned by the current user. |
| `GET` | `/uploads/garments/{fileName}` | Serve uploaded garment files. This route is outside `/api`. |
| `GET` | `/api/storage/signed/{objectKey}` | Serve a signed local object URL until its expiry. S3/MinIO presigned URLs go directly to object storage. |
| `GET` | `/uploads/body-reference-photos/{fileName}` | Disabled privacy compatibility route; returns 404. |

The current user is resolved from the server-side auth session, not from a browser-supplied user header.

## Verification

Backend tests are a console test runner:

```powershell
dotnet run --project outfit_planner_back\tests\OutfitPlanner.Api.Tests\OutfitPlanner.Api.Tests.csproj
```

Build the API:

```powershell
dotnet build outfit_planner_back\src\OutfitPlanner.Api\OutfitPlanner.Api.csproj
```

Frontend tests and build:

```powershell
cd outfit_planner_front
npm test
npm run build
```

Docker smoke checks:

```powershell
docker compose -f docker-compose.dev.yml up -d --build
Invoke-RestMethod http://localhost:5000/api/health
```

## Current Boundaries

- Google and Apple auth require provider credentials. Email/password auth works without external secrets.
- Password registration requires at least 8 characters, at least one letter, and at least one digit.
- Uploaded files are stored on local disk, not MinIO. MinIO is present as an infrastructure placeholder.
- PostgreSQL schema creation is startup-time schema initialization, not a full migration system.
- Only `Top` and `Bottom` garment categories are supported.
- The mock try-on provider is the default. FASHN, Replicate, Fal, and local VTON providers require explicit environment configuration.

## Troubleshooting

- If the frontend shows network failures, confirm the API is available at `http://localhost:5000/api/health`.
- If running frontend dev against a non-default API port, set `VITE_DEV_API_TARGET`.
- If PostgreSQL connection fails from local Windows development, confirm the compose database is reachable on host port `5433`, not `5432`.
- If photo upload fails through Docker production frontend, check nginx `client_max_body_size` and the API upload diagnostics in logs.
- If FASHN returns no result for multi-garment outfits, enable `Sequential flow` in the Builder UI or test with a single garment.
