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

## Project Context

- Backend is ASP.NET Core Minimal API on .NET 10.
- Backend projects live under `outfit_planner_back/src/`:
  - `OutfitPlanner.Domain`: entities, enums, domain rules.
  - `OutfitPlanner.Application`: use-case services and abstractions.
  - `OutfitPlanner.Infrastructure`: PostgreSQL store, local photo storage, security/time helpers, mock/FASHN try-on providers.
  - `OutfitPlanner.Api`: route mapping, dependency injection, CORS, upload limits, diagnostics.
- Backend storage is selected at startup:
  - Empty `ConnectionStrings__Postgres`: in-memory store.
  - Non-empty `ConnectionStrings__Postgres`: PostgreSQL store and startup schema initialization from `database/schema.sql`.
- Uploaded files are served from API routes outside `/api`: `/uploads/garments/{fileName}` and `/uploads/body-reference-photos/{fileName}`.
- Frontend is React + TypeScript + Vite under `outfit_planner_front/`.
- Frontend state/data uses TanStack Query; routing uses React Router.
- Main UI surfaces are Wardrobe, Builder, Calendar, and shared outfit view.
- Frontend visual system is High-Fidelity Claymorphism in `src/styles.css`: Nunito headings, DM Sans body, lavender canvas, animated blobs, large rounded clay panels, recessed inputs, convex gradient buttons, and 4-layer shadows.
- `src/App.tsx` should preserve Wardrobe, Builder, Calendar, and Share routes while keeping service metadata endpoints available in the shell and try-on job status available after generation.
- Demo identity uses `X-Demo-User`; invalid or missing values fall back to `demo-user`.
- Try-on defaults to `MockTryOnProvider`. FASHN is opt-in with `TryOn__Provider=Fashn` and `Fashn__ApiKey`.
- Multi-garment FASHN generation needs the Builder page `Sequential flow` toggle.

## Common Commands

Run backend with in-memory storage:

```powershell
dotnet run --project outfit_planner_back\src\OutfitPlanner.Api\OutfitPlanner.Api.csproj --urls http://localhost:5000
```

Run backend against compose PostgreSQL:

```powershell
docker compose -f docker-compose.dev.yml up -d postgres minio
$env:ConnectionStrings__Postgres = "Host=localhost;Port=5433;Database=outfit_planner;Username=outfit;Password=outfit;GSS Encryption Mode=Disable"
dotnet run --project outfit_planner_back\src\OutfitPlanner.Api\OutfitPlanner.Api.csproj --urls http://localhost:5000
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

Run frontend dev server:

```powershell
cd outfit_planner_front
npm run dev
```

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
docker compose -f docker-compose.dev.yml up --build
```

Run production-style stack:

```powershell
docker compose up --build
```
