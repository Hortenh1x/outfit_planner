# Outfit Planner

Outfit Planner is a research-demo web app for cataloging garments, composing outfits, planning outfits by day, sharing saved outfits, and generating AI try-on previews through a replaceable provider adapter.

The app is intentionally small, but it has a real backend/frontend split, local photo upload storage, optional PostgreSQL persistence, and an optional FASHN provider scaffold. By default it uses in-memory storage and the mock try-on provider, so local development does not need paid AI credentials.

## Features

- Wardrobe catalog for top and bottom garments.
- Garment photo uploads from the browser.
- Body reference photo uploads for try-on generation.
- Outfit builder with one garment per supported category.
- Clothes-only and generated person preview modes.
- Calendar planning with one outfit per user and day.
- Share links for saved outfits.
- Secure account registration and sign-in with email/password.
- Google OAuth and Apple OIDC sign-in when provider credentials are configured.
- Revocable server-side sessions with HttpOnly cookies and CSRF protection.
- Mock AI try-on by default, optional FASHN `tryon-v1.6` integration.

## Tech Stack

- Backend: ASP.NET Core Minimal API on .NET 10.
- Backend architecture: onion-style `Domain`, `Application`, `Infrastructure`, and `Api` projects.
- Persistence: PostgreSQL through Npgsql when `ConnectionStrings__Postgres` is configured; in-memory fallback otherwise.
- File storage: local disk under the API storage directory.
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
|   |-- database/schema.sql
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
- `OutfitPlanner.Infrastructure` implements PostgreSQL storage, local photo storage, clocks, password hashing, auth token hashing, share token generation, and try-on providers.
- `OutfitPlanner.Api` wires dependencies, JSON/CORS/upload limits, routes, diagnostics, secure auth cookies, OAuth/OIDC callbacks, and CSRF enforcement.

The API chooses storage at startup:

- Empty `ConnectionStrings:Postgres`: use `InMemoryOutfitStore`.
- Non-empty `ConnectionStrings:Postgres`: use `PostgresOutfitStore` and apply `outfit_planner_back/database/schema.sql` on startup.

The frontend uses a same-origin API path by default:

- `VITE_API_URL` defaults to `/api`.
- Vite proxies `/api` and `/uploads` to `VITE_DEV_API_TARGET` or `http://localhost:5000`.
- The production Docker frontend builds with `VITE_API_URL=/api` and nginx proxies `/api/` and `/uploads/` to the API service.

Authentication is cookie-backed:

- Email/password registration and sign-in create an opaque server-side session.
- Session cookie `outfit_session` is HttpOnly, SameSite=Lax, and Secure outside development.
- CSRF cookie `outfit_csrf` is readable by the frontend and must be echoed as `X-CSRF-Token` on mutating authenticated API requests.
- Google and Apple sign-in start from backend challenge endpoints and complete through backend callbacks. If the external account is new, the API creates it automatically. If the provider returns a verified email that already exists, the external login is linked to that user.
- All private `/api` routes require a valid session. `/api/health`, `/api/system/status`, `/api/auth/*`, and `/api/share/{token}` remain public.

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

Run PostgreSQL and MinIO only:

```powershell
docker compose -f docker-compose.dev.yml up -d postgres minio
```

Run the API against the compose PostgreSQL service:

```powershell
$env:ConnectionStrings__Postgres = "Host=localhost;Port=5433;Database=outfit_planner;Username=outfit;Password=outfit;GSS Encryption Mode=Disable"
dotnet run --project outfit_planner_back\src\OutfitPlanner.Api\OutfitPlanner.Api.csproj --urls http://localhost:5000
```

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
| `TryOn__Provider` | `Mock` | Use `Mock` or `Fashn`. Anything other than `Fashn` uses the mock provider. |
| `Fashn__ApiKey` | empty | Required before the FASHN provider makes network calls. |
| `Fashn__BaseUrl` | `https://api.fashn.ai/v1/` | FASHN API base URL. |
| `Fashn__ModelName` | `tryon-v1.6` | FASHN model name. |
| `Fashn__Mode` | `balanced` | FASHN generation mode. |
| `Fashn__MaxPollingAttempts` | `30` | Status polling limit. |
| `Fashn__PollIntervalSeconds` | `2` | Delay between status polls. |
| `Fashn__TimeoutSeconds` | `180` | HTTP client timeout. |
| `DetailedErrors` | environment-dependent | Enables structured exception details in dev/test diagnostics. |
| `Authentication__Google__ClientId` | empty | Enables Google OAuth when paired with `Authentication__Google__ClientSecret`. |
| `Authentication__Google__ClientSecret` | empty | Google OAuth client secret. |
| `Authentication__Apple__ClientId` | empty | Enables Apple OIDC when paired with `Authentication__Apple__ClientSecret`. |
| `Authentication__Apple__ClientSecret` | empty | Apple OIDC client secret JWT generated from Apple developer credentials. |

Frontend configuration:

| Setting | Default | Purpose |
| --- | --- | --- |
| `VITE_API_URL` | `/api` | Base URL used by `src/api/client.ts`. |
| `VITE_DEV_API_TARGET` | `http://localhost:5000` | Vite dev proxy target for `/api` and `/uploads`. |

## Optional FASHN Try-On Provider

The backend uses the mock try-on provider by default. The mock returns deterministic demo output and does not spend credits.

Enable the FASHN scaffold:

```powershell
$env:TryOn__Provider = "Fashn"
$env:Fashn__ApiKey = "YOUR_FASHN_API_KEY"
dotnet run --project outfit_planner_back\src\OutfitPlanner.Api\OutfitPlanner.Api.csproj --urls http://localhost:5000
```

The FASHN provider submits to `/run` and polls `/status/{id}`. A single-garment outfit maps directly to one provider run. Multi-garment outfits require the Builder page's `Sequential flow` toggle; the provider then applies garments one after another, using the previous output image as the next model image. This can consume one provider run per garment.

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
| `GET` | `/auth/external/{provider}/start` | Start Google or Apple auth with `returnUrl`. |
| `GET` | `/auth/external/{provider}/callback` | Complete backend OAuth/OIDC callback and issue auth cookies. |
| `GET` | `/body-reference-photos` | List body reference photos for the current user. |
| `POST` | `/body-reference-photos` | Register an already uploaded body reference photo URL. |
| `DELETE` | `/body-reference-photos/{photoId}` | Delete a body reference photo and its stored file when local. |
| `GET` | `/garments` | List garments for the current user. |
| `POST` | `/garments` | Create a garment. |
| `DELETE` | `/garments/{garmentId}` | Delete a garment and its stored file when local. |
| `POST` | `/uploads/garment-photo` | Multipart garment photo upload. |
| `POST` | `/uploads/body-reference-photo` | Multipart body reference photo upload. |
| `GET` | `/outfits` | List saved outfits. |
| `POST` | `/outfits` | Create an outfit. |
| `POST` | `/outfits/{outfitId}/try-on` | Start try-on generation. |
| `GET` | `/try-on-jobs/{jobId}` | Read try-on job status/result. |
| `POST` | `/schedule` | Plan an outfit for a date. |
| `GET` | `/schedule?from=YYYY-MM-DD&to=YYYY-MM-DD` | List planned outfits for a date range. |
| `POST` | `/outfits/{outfitId}/share` | Create a share link. |
| `GET` | `/share/{token}` | Read a shared outfit. |
| `GET` | `/uploads/garments/{fileName}` | Serve uploaded garment files. This route is outside `/api`. |
| `GET` | `/uploads/body-reference-photos/{fileName}` | Serve uploaded body reference files. This route is outside `/api`. |

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
- The mock try-on provider is the default. FASHN requires explicit environment configuration.

## Troubleshooting

- If the frontend shows network failures, confirm the API is available at `http://localhost:5000/api/health`.
- If running frontend dev against a non-default API port, set `VITE_DEV_API_TARGET`.
- If PostgreSQL connection fails from local Windows development, confirm the compose database is reachable on host port `5433`, not `5432`.
- If photo upload fails through Docker production frontend, check nginx `client_max_body_size` and the API upload diagnostics in logs.
- If FASHN returns no result for multi-garment outfits, enable `Sequential flow` in the Builder UI or test with a single garment.
