# Outfit Planner

Outfit Planner is a research-demo web app for cataloging garments, composing outfits, planning outfits by day, sharing saved outfits, and generating AI try-on previews through a replaceable provider adapter.

The app is intentionally small, but it has a real backend/frontend split, signed object storage for uploads, optional PostgreSQL persistence, and an optional FASHN provider scaffold. By default it uses in-memory storage, local object storage, and the mock try-on provider, so local development does not need paid AI credentials.

## Features

- Wardrobe catalog for Top, Bottom, Dress, Outerwear, Shoes, Bag, and Accessory garments with editable structured metadata for colors, material, brand, size, season, weather, occasion, scoring, favorites, archive state, last worn date, and laundry status.
- Global hairstyle presets (openly licensed, gender-filtered, single hair color) served by the API in place of the retired Hat garment category; presets are app-owned catalog data, not uploads, and are never sent to AI try-on.
- Garment photo uploads from the browser.
- Wardrobe uses an editorial Obra/Crimson-inspired interface for catalog search, filters, edit, archive, bulk upload, drag-and-drop upload, mobile camera capture, clean photo guidance, local tag suggestions, and photo quality warnings. Garment cards show the photo only; Edit and Delete are revealed on hover, keyboard focus, or touch press-and-hold.
- Private body reference photo uploads for try-on generation.
- Outfit builder with slot compatibility rules instead of one-garment-per-category rules.
- Composed-figure clothes-only Builder: a neutral gendered SVG silhouette wears the outfit fitted onto fixed body zones (garments anchored to the shoulder/waist/feet body lines and sized from their measured cutout so they sit on the body and layer correctly); every category is pickable from the Wardrobe pieces list, with top/bottom/shoes additionally cyclable directly on the figure (touch swipe + desktop arrows), and the composition (with the silhouette gender) is saved with the outfit so saved-outfit cards and the shared view render the exact same figure. (Hairstyle presets exist behind the scenes but are currently hidden from the product.)
- Clothes-only and generated person preview modes.
- Calendar planning with one outfit per user and day.
- Share links for saved outfits.
- Secure account registration and sign-in with email/password.
- Google OAuth and Apple OIDC sign-in when provider credentials are configured.
- Revocable server-side sessions with HttpOnly cookies, CSRF protection, rate-limited auth endpoints, email verification/password reset token storage, and session revoke-all support.
- Free, Premium, and Admin account roles with two email-pinned accounts whose roles can never change, and an admin panel that lists, inspects, and manages all users, their data, and their AI credits.
- An enforced paywall over AI compute ([`PAYWALL_MODEL.md`](PAYWALL_MODEL.md)): per-tier plan catalog (free caps on garments/outfits/body photos, allowed try-on modes, output resolution), an AI-credit ledger with a one-time 8-credit free trial grant (top-up-to-config for existing accounts) and a rolling monthly Premium allowance, debit-on-confirm with automatic refunds for failed jobs, no spend on cache hits, a premium-priority try-on queue, and entitlements surfaced in the Builder (credits chip, Premium mode pills, upgrade notice).
- Stripe billing implemented to "insert the API key" readiness: subscription checkout, credit top-up packs, the customer portal, signature-verified idempotent webhooks that mirror subscription state and switch `Free ↔ Premium` roles automatically (pinned accounts exempt), an `/upgrade` page, and read-only admin subscription visibility. Without `Stripe__SecretKey` billing reads as disabled and roles are still switched manually via the admin panel.
- Privacy endpoints for account export/delete, body photo deletion, and AI output purging.
- Configurable garment background removal for uploaded item cutouts, with simple local fallback, `rembg`, and HTTP/API provider adapters.
- Optional local garment auto-tagging that prefills category, colors, seasons, and tags on upload (FashionCLIP + k-means colors), with soft degradation when the service is off and user edits that are never overwritten.
- Background AI try-on jobs with a Redis-backed queue in Docker and an in-memory queue fallback for local development.
- Mock AI try-on by default, optional FASHN `tryon-max`, local VTON/CatVTON, Replicate, and Fal provider adapters.
- Installable PWA shell with manifest metadata, static shell caching, offline fallback, and responsive mobile bottom navigation.

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
        |-- app/
        |-- routes/
        |-- api/
        |-- components/
        |-- features/
        |-- shared/
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
- Garment uploads store original, thumbnail, processed cutout, optional segmentation mask, and perceptual hash. The upload response exposes variant URLs, and new wardrobe items use the processed cutout as their primary image.
- Garment cutouts are trimmed to their garment's main connected opaque region and measured (`cutoutWidthPx`/`cutoutHeightPx`, persisted in `garment_items` and exposed on the garment DTO and upload response), so leftover background grain in a corner cannot inflate the measured garment while the whole garment is always kept. The absolute pixels depend on the shot, but height/width is invariant to shooting distance, so the frontend `computeRelativeSize` utility (`src/features/outfits/relativeSize.ts`) renders garments at per-category canonical relative sizes: Top/Bottom/Dress/Outerwear normalize by width (display height expresses garment length), Shoes/Bag/Accessory fit a fixed box by their larger side. Measurement refreshes on rotation re-render and async background removal; a startup `GarmentCutoutMeasurementBackfillWorker` measures existing garments best-effort (cutout variant first, then original).
- Global hairstyle presets replace hat garments: `GET /api/hairstyles` lists presets filtered by the account's gender (empty until gender is set), and `GET /api/hairstyles/assets/{fileName}` serves the SVG assets anonymously. The presets live in `outfit_planner_back/assets/hairstyles` with `manifest.json` as the source of truth (only manifest-listed files are served). Assets are vendored from Open Peeps by Pablo Stanley (CC0 1.0, via the `@dicebear/open-peeps` npm package), curated to ~10 male and ~10 female styles in a single hair color, excluding afro variants and head-wear variants. Migration 009 deletes legacy Hat garments and rebuilds the category constraints.
- Garment cutouts use `BackgroundRemoval__Provider`; the default `Auto` provider uses local `rembg` when the executable is available and otherwise falls back to the dependency-free `Simple` provider. `Rembg` can be selected explicitly for one CLI process per upload, `RembgServer` targets a long-running local `rembg s` server, and `Http`/provider aliases call an API that returns a transparent image. Garment extraction currently assumes one clothing item per upload through a single-item provider boundary; multi-item detection/separation is intentionally not active yet.
- Body reference uploads store original, thumbnail, blurred private preview, and perceptual hash. Account avatars store private original and thumbnail variants. Public `/uploads/body-reference-photos/{fileName}` access is disabled; clients receive signed object URLs.
- Local signed object URLs are refreshed when wardrobe, body-reference, outfit, share, and try-on flows read saved records, so persisted local uploads keep rendering after URL expiry or API restarts.

Try-on jobs are queued at request time:

- Empty `ConnectionStrings:Redis`: use an in-memory try-on queue.
- Non-empty `ConnectionStrings:Redis`: use Redis list queue `outfit-planner:try-on-jobs`.
- `POST /api/outfits/{outfitId}/try-on` creates a `Queued` job and returns `202 Accepted`; a hosted background worker moves the job through `Processing` to `Succeeded` or `Failed`.

The frontend uses a same-origin API path by default:

- `VITE_API_URL` defaults to `/api`.
- Vite runs over HTTPS by default and proxies `/api` and `/uploads` to `VITE_DEV_API_TARGET` or `https://localhost:5001`.
- The production Docker frontend builds with `VITE_API_URL=/api`, terminates public TLS in nginx, and proxies `/api/` and `/uploads/` to the API service over the internal Docker network.

Authentication is cookie-backed:

- Email/password registration and sign-in create an opaque server-side session.
- Session cookie `outfit_session` is HttpOnly, SameSite=Lax, and Secure outside development.
- CSRF cookie `outfit_csrf` is readable by the frontend and must be echoed as `X-CSRF-Token` on mutating authenticated API requests.
- Account settings persist `username`, optional signed avatar URL, and `gender` (`Male` or `Female`) on the backend. AI try-on modes are unavailable until the authenticated user has selected a gender; clothes-only preview remains available.
- Every account has a role (`Free` by default, `Premium`, or `Admin`) exposed as `role` on the session user. The effective role pins `dmytro.bolibok@gmail.com` to `Admin` and `olya.shaydur@gmail.com` to `Premium` by normalized email (configurable through `Roles__PinnedAdminEmails`/`Roles__PinnedPremiumEmails`; blank config keeps these defaults), and sign-in converges the stored role in every storage backend. Admins see the `/admin` panel; `/api/admin/*` routes return `403` for other roles, and pinned accounts can be neither re-roled nor deleted there.
- Google and Apple sign-in start from backend challenge endpoints and complete through backend callbacks. If the external account is new, the API creates it automatically. If the provider returns a verified email that already exists, the external login is linked to that user.
- All private `/api` routes require a valid session. `/api/health`, `/api/system/status`, `/api/auth/*`, `/api/storage/signed/*`, and `/api/share/{token}` remain public; signed storage access is protected by URL signature and expiry.

The frontend uses an editorial fashion/product visual system across authenticated surfaces:

- Shared editorial component styles live in `outfit_planner_front/src/styles.css` for Auth, Builder, Calendar, Share, and reusable UI helpers.
- The authenticated shell and Wardrobe slice use scoped editorial CSS with warm paper and dark ink themes, serif display headings, crimson emphasis, hairline borders, flat panels, restrained shadows, compact controls, and tactile crimson primary buttons.
- The canonical light palette and typography come from `design_references/light_theme` Crimson Plinth tokens; `design_references/dark_theme` is the dark orientation, with the same pink primary actions preserved in dark mode.
- Wardrobe filtering should use category tabs as the single category control, keep search in the compact top control row, and expose existing tags through a writable tag combobox. On mobile, creation/planning rails come before browsing content, while the authenticated account shell sits below page content.
- Wardrobe garment cards are image-only: the photo fills the card, and Edit and Delete are revealed only on hover (desktop), keyboard focus, or press-and-hold (touch), while functional status badges (background removing/failed, needs-better-photo) overlay the photo. The like and duplicate card actions and the Favorites filter were removed; the tag system (filters, editor, upload chips) is unchanged.
- Wardrobe quick-build: each minimized card has a top-right selection checkmark and opens the full photo enlarged on tap (view-only dialog, closes on backdrop/close/Esc). Picking is at most one garment per category; a floating build bar appears while pieces are selected and carries the set into the Builder clothes-only screen through router state. The Builder wears the picked pieces (a picked dress is worn while the picked top/bottom are remembered), and any unpicked base Top/Bottom/Shoes fall back to the first item of their category, while Dress/Outerwear/Bag/Accessory join only when explicitly picked.
- The Wardrobe upload queue removes backgrounds as soon as photos are selected (not on submit): each row processes in the background with a limited concurrency, shows the transparent cutout when ready, and submit only creates garments from the already-processed result. Upload-queue tags are edited as interactive chips — click a chip to rename it, hover/focus to reveal a trash delete, and add tags from the trailing input — replacing the old comma-separated tags field.
- Builder controls prioritize body references and try-on generation before the save-outfit block. A generated try-on preview is saved onto the outfit by default and shown on its saved-outfit card; a metadata-only save (rename, tags, favorite) keeps it, and only changing the worn garments clears it. Clicking a saved-outfit card enlarges it in a view-only dialog (the generated preview, or the composed figure when there is none) with an `Open in builder` action; from there the selected outfit can be edited, deleted, or regenerated, and the latest generated try-on preview can be removed from the active outfit.
- Calendar mobile layout puts Plan day before the calendar grid, selected current-day numbers must stay high-contrast, and planned outfits can be reassigned or removed from a date.
- Do not extend the removed claymorphism system; lavender canvases, animated blobs, oversized rounded panels, recessed controls, convex purple gradients, and multi-layer neumorphic shadows are no longer part of the active frontend language.
- Frontend composition is split across `outfit_planner_front/src/app`, route pages under `src/routes`, feature components under `src/features`, and reusable UI under `src/shared/ui`. `src/App.tsx` remains a compatibility export.
- Frontend API response types are generated from the backend OpenAPI document into ignored local artifacts and re-exported through committed aliases.

## Prerequisites

- .NET 10 SDK.
- Node.js 24 or a recent Node version compatible with the locked frontend dependencies.
- Docker Desktop or another Docker Compose runtime for PostgreSQL/MinIO/container workflows.

## Quick Start With Docker

Create and trust the local development HTTPS certificate before running the dev compose stack for the first time:

```powershell
New-Item -ItemType Directory -Force .aspnet\https
dotnet dev-certs https --trust
dotnet dev-certs https -ep .aspnet\https\outfit-planner-dev.pfx -p outfit-dev-cert
```

Local dev stack with `rembg` background removal:

```powershell
docker compose -f docker-compose.dev.yml -f docker-compose.rembg.dev.yml up -d
```

Publicly-hosted dev stack with `rembg` (adds real AI background removal plus FASHN/Google/Apple
credentials wired from `.env`, behind a Cloudflare Tunnel, see [`DEPLOY-CLOUDFLARE.md`](DEPLOY-CLOUDFLARE.md)):

```powershell
docker compose -f docker-compose.dev.yml -f docker-compose.selfhost.override.yml up -d
```

Open:

- Frontend: `https://localhost:5173`
- API: `https://localhost:5001/api/health`
- PostgreSQL on host: `localhost:5433`
- Redis on host: `localhost:6379`
- MinIO API: `http://localhost:9000`
- MinIO console: `http://localhost:9001`

## Google/Apple OAuth Setup

Google and Apple sign-in are enabled only when their provider credentials are configured (see the Configuration table). The browser-facing origin is `https://localhost:5173`.

In Google Cloud Console, add this exact Authorized JavaScript origin:

```text
https://localhost:5173
```

Also add this exact Authorized redirect URI:

```text
https://localhost:5173/api/auth/external/google/callback
```

The provider callback path is only for Google/Apple to return to the API. After the provider callback succeeds, the API redirects internally to `/api/auth/external/{provider}/complete` to issue app cookies and then returns to the requested app route.

## Configuration

Backend configuration can be supplied through `appsettings.json`, environment variables, or Docker Compose.

| Setting | Default | Purpose |
| --- | --- | --- |
| `ConnectionStrings__Postgres` | empty | Enables PostgreSQL persistence when non-empty. |
| `ConnectionStrings__Redis` | empty | Enables the Redis try-on job queue when non-empty. Empty uses an in-memory queue. |
| `ObjectStorage__Provider` | `Local` | Use `Local`, `S3`, or `Minio` for uploaded image object storage. |
| `ObjectStorage__Local__Root` | `storage/objects` under API content root | Local object storage root. |
| `ObjectStorage__Local__SigningSecret` | development fallback (Development only) | HMAC secret for local signed object URLs. **Required outside Development**: the API fails to start with the `Local` provider when unset, preventing signed-URL forgery with the source-visible dev key. |
| `ObjectStorage__S3__Endpoint` / `Minio__Endpoint` | empty | S3-compatible endpoint for MinIO/S3 storage. |
| `ObjectStorage__S3__AccessKey` / `Minio__AccessKey` | empty | S3-compatible access key. |
| `ObjectStorage__S3__SecretKey` / `Minio__SecretKey` | empty | S3-compatible secret key. |
| `ObjectStorage__S3__Bucket` / `Minio__Bucket` | `outfit-planner-private` | Private bucket for uploaded image variants. |
| `BackgroundRemoval__Provider` | `Auto` | Use `Auto`, `Simple`, `Rembg`, `RembgServer`, `Http`, `CloudflareImages`, `PhotoRoom`, `RemoveBg`, or `Clipdrop` for garment cutout generation. `Auto` uses `rembg` when available and falls back to `Simple`; unknown values use `Simple`. |
| `BackgroundRemoval__Rembg__ExecutablePath` | `rembg` | Local executable used when `BackgroundRemoval__Provider=Auto` or `Rembg`. |
| `BackgroundRemoval__Rembg__ModelName` | `birefnet-general` | Local `rembg` model. Use `birefnet-general-lite` if CPU runtime is too slow. |
| `BackgroundRemoval__Rembg__ModelHome` | empty | Optional model cache directory passed as `U2NET_HOME` for `rembg`. |
| `BackgroundRemoval__Rembg__TimeoutSeconds` | `180` | Local `rembg` process timeout. |
| `BackgroundRemoval__RembgServer__Endpoint` | `http://127.0.0.1:7000/api/remove` | Long-running `rembg s` remove endpoint used when `BackgroundRemoval__Provider=RembgServer`. |
| `BackgroundRemoval__RembgServer__ImageFieldName` | `file` | Multipart field name expected by the `rembg s` remove endpoint. |
| `BackgroundRemoval__RembgServer__ModelName` | `birefnet-general` | Model sent to the `rembg s` endpoint as form field `model`. Falls back to `BackgroundRemoval__Rembg__ModelName` when unset. |
| `BackgroundRemoval__RembgServer__TimeoutSeconds` | `120` | HTTP timeout for the local `rembg s` provider. |
| `BackgroundRemoval__Http__Endpoint` | empty | Multipart background-removal endpoint used by `Http` and as a fallback for HTTP provider aliases. Must return image bytes, preferably transparent PNG. |
| `BackgroundRemoval__Http__ApiKey` | empty | API key for the HTTP background-removal endpoint. |
| `BackgroundRemoval__Http__ApiKeyHeader` | `X-Api-Key` | Header name for the HTTP API key. `CloudflareImages` defaults to `Authorization`. |
| `BackgroundRemoval__Http__ApiKeyPrefix` | empty | Prefix prepended to the API key header value. `CloudflareImages` defaults to `Bearer `. |
| `BackgroundRemoval__Http__ImageFieldName` | `image_file` | Multipart field name for the uploaded image. |
| `BackgroundRemoval__Http__TimeoutSeconds` | `120` | HTTP background-removal timeout. |
| `AutoTagging__Provider` | `Auto` | Use `Auto`, `Disabled`, or `HttpServer` for garment metadata prefill. `Auto` uses the local auto-tag service when its `/health` endpoint is reachable and otherwise disables prefill; `Disabled` turns suggestions off; unknown values disable. |
| `AutoTagging__HttpServer__Endpoint` | `http://127.0.0.1:7100/classify` | Local auto-tag service classify endpoint. |
| `AutoTagging__HttpServer__HealthEndpoint` | derived from Endpoint | Health-probe URL used by `Auto` routing; defaults to the Endpoint origin's `/health`. |
| `AutoTagging__HttpServer__TimeoutSeconds` | `60` | HTTP timeout for a classification request. |
| `AutoTagging__HttpServer__HealthCacheSeconds` | `15` | How long a health-probe result is cached before re-checking. |
| `AutoTagging__HttpServer__HealthTimeoutSeconds` | `3` | HTTP timeout for the health probe. |
| `Billing__Provider` | `Auto` | Use `Auto`, `Stripe`, or `Disabled` for paywall billing. `Auto` selects Stripe when `Stripe__SecretKey` is set and otherwise disables billing softly (the app works unchanged). |
| `Stripe__SecretKey` | empty | Stripe API secret key. Required before any billing network call; never commit it. |
| `Stripe__WebhookSecret` | empty | Signing secret of the Stripe webhook endpoint pointing at `/api/billing/webhook`. |
| `Stripe__PremiumMonthlyPriceId` | empty | Stripe price id of the Premium monthly subscription. Checkout is rejected until set. |
| `Stripe__PremiumMonthlyDisplayPrice` | empty | Optional display price (e.g. `$9/mo`) shown on the upgrade page; Stripe Checkout stays the source of truth. |
| `Stripe__TopUpPacks__N__{Id,Credits,PriceId,DisplayPrice}` | 20/50/100-credit packs, no price ids | Credit top-up packs; packs without a `PriceId` are not offered. |
| `Stripe__SuccessUrl` / `Stripe__CancelUrl` / `Stripe__PortalReturnUrl` | derived | Default to `Authentication__PublicOrigin` + `/upgrade?checkout=success|cancelled` and `/upgrade`. |
| `TryOn__Provider` | `Mock` | Use `Mock`, `Fashn`, `CompositeFashn`, `LocalVton`, `LocalCatVton`, `SelfHostedCatVton`, `GeneralImageEdit`, `Replicate`, or `Fal`. Unknown values use the mock provider. |
| `Fashn__ApiKey` | empty | Required before the FASHN provider makes network calls. |
| `Fashn__BaseUrl` | `https://api.fashn.ai/v1/` | FASHN API base URL. |
| `Fashn__ModelName` | `tryon-max` | FASHN model name used by the API for paid try-on. |
| `Fashn__Mode` | `quality` | FASHN generation mode. `quality` is used with `tryon-max` for the highest quality path. |
| `Fashn__MaxPollingAttempts` | `30` | Status polling limit. |
| `Fashn__PollIntervalSeconds` | `2` | Delay between status polls. |
| `Fashn__TimeoutSeconds` | `180` | HTTP client timeout. |
| `Fashn__NumSamples` | `1` | Number of FASHN samples to request. |
| `Fashn__OutputFormat` | `png` | FASHN output image format. |
| `Fashn__ReturnBase64` | `false` | Whether FASHN should return base64 image data. |
| `Fashn__SegmentationFree` | `true` | Whether FASHN should use segmentation-free garment processing. |
| `Fashn__GarmentPhotoType` | `auto` | FASHN garment photo type hint. |
| `Fashn__Seed` | empty | Optional FASHN generation seed. |
| `Fashn__Resolution` | `4k` | FASHN `tryon-max` output resolution; controls credits per run (`1k` = 2 credits, `4k` = 5). |
| `Fashn__GenderPromptTemplate` | empty | Optional opt-in prompt for `tryon-max`. Empty by default, so no prompt is sent and the model preserves the person's identity and gender from the body photo. When set, `{gender}` is replaced with `male`/`female`; intended for garment-styling experiments only. |
| `FASHN_*` `.env` aliases | empty | Repository `.env` keys such as `FASHN_API_KEY`, `FASHN_BASE_URL`, `FASHN_MODEL_NAME`, `FASHN_MODE`, `FASHN_NUM_SAMPLES`, `FASHN_OUTPUT_FORMAT`, `FASHN_RETURN_BASE64`, `FASHN_SEGMENTATION_FREE`, `FASHN_GARMENT_PHOTO_TYPE`, `FASHN_SEED`, `FASHN_RESOLUTION`, and `FASHN_GENDER_PROMPT_TEMPLATE` are mapped to the matching `Fashn__*` settings at API startup. |
| `TryOn__CompositeFashn__ApiKey` | empty | Required when `TryOn__Provider=CompositeFashn`. |
| `TryOn__LocalVton__BaseUrl` | `http://localhost:7860/` | Local/dev VTON endpoint base URL. |
| `TryOn__LocalVton__Endpoint` | `/try-on` | Local/dev VTON generation endpoint. |
| `TryOn__LocalCatVton__BaseUrl` | `http://localhost:7861/` | Local/dev CatVTON endpoint base URL. |
| `TryOn__LocalCatVton__Endpoint` | `/try-on` | Local/dev CatVTON generation endpoint. |
| `TryOn__SelfHostedCatVton__BaseUrl` | `http://localhost:7861/` | Self-hosted CatVTON endpoint base URL. |
| `TryOn__GeneralImageEdit__ApiKey` | empty | Required when `TryOn__Provider=GeneralImageEdit`. |
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
| `Roles__PinnedAdminEmails` | `dmytro.bolibok@gmail.com` | Comma-separated emails whose accounts are always `Admin`. Blank keeps the default pin. |
| `Roles__PinnedPremiumEmails` | `olya.shaydur@gmail.com` | Comma-separated emails whose accounts are always `Premium`. Blank keeps the default pin. |
| `Paywall__Free__MaxGarments` | `50` | Free-tier garment cap. Zero or negative means unlimited. |
| `Paywall__Free__MaxOutfits` | `20` | Free-tier saved-outfit cap. Zero or negative means unlimited. |
| `Paywall__Free__MaxBodyReferencePhotos` | `1` | Free-tier body reference photo cap. Zero or negative means unlimited. |
| `Paywall__Free__TrialCredits` | `6` | One-time AI-credit trial grant for free accounts. |
| `Paywall__Premium__MaxBodyReferencePhotos` | `5` | Premium body reference photo cap. Zero or negative means unlimited. |
| `Paywall__Premium__MonthlyCredits` | `100` | Premium AI-credit allowance granted once per UTC calendar month (rolls over). |

Frontend configuration:

| Setting | Default | Purpose |
| --- | --- | --- |
| `VITE_API_URL` | `/api` | Base URL used by `src/api/client.ts`. |
| `VITE_DEV_API_TARGET` | `https://localhost:5001` | Vite dev proxy target for `/api` and `/uploads`. |
| `VITE_DEV_HTTPS_PFX` | empty | Optional PFX certificate path for Dockerized HTTPS Vite dev server. |
| `VITE_DEV_HTTPS_PFX_PASSPHRASE` | empty | Passphrase for `VITE_DEV_HTTPS_PFX`. |

## Garment Background Removal

Garment photo uploads always create original, thumbnail, processed cutout, and segmentation mask variants. The upload response includes `originalUrl`, `thumbnailUrl`, `cutoutUrl`, and `maskUrl`; the frontend stores `cutoutUrl` as the garment image and `thumbnailUrl` as the card thumbnail. By default, the backend uses `Auto` background removal: local `rembg` when it is available, otherwise the dependency-free `Simple` fallback. Real photos on textured backgrounds need `rembg` locally or an HTTP provider in production; the simple fallback is only a development safety net.

The extraction layer currently assumes exactly one garment per upload. `SingleGarmentExtractionProvider` is a placeholder boundary around background removal so a future detector can return multiple candidates without changing the lower-level image variant pipeline.

The dev stacks above run `rembg` as a container and point the API at it through `BackgroundRemoval__Provider=RembgServer` (see `docker-compose.rembg.dev.yml` and `docker-compose.selfhost.override.yml`), so no separate `rembg` process is needed. All `BackgroundRemoval__*` settings — including the `Http`/API providers for production — are listed in the Configuration table.

`CloudflareImages`, `PhotoRoom`, `RemoveBg`, and `Clipdrop` are HTTP aliases with provider-specific config sections such as `BackgroundRemoval__CloudflareImages__Endpoint`. For the cheapest Cloudflare path, point `CloudflareImages` at a small Worker/API endpoint that accepts multipart field `image_file`, uses Cloudflare image foreground segmentation, and returns transparent image bytes.

## Garment Auto-Tagging

Uploaded garment photos can be auto-classified into prefill suggestions — category, dominant colors, seasons, and tags — so the wardrobe upload queue fills metadata for you. Suggestions are hints only: you can always override them, and any field you edit is frozen and never overwritten (the per-field freeze generalizes the existing tag freeze).

Classification runs on a separate, local Python service and is orchestrated by the frontend after each row's photo is processed (concurrency-limited, and aborted if the row is removed), mirroring the eager background-removal flow. It never blocks the upload endpoints. The backend `POST /api/uploads/garment-photo/classify` endpoint reads the already-uploaded photo, prefers an existing transparent cutout (generating one from the original when none exists yet), and forwards a clean cutout to the service. Everything runs locally; images are never sent to an external API.

### Models and licenses

- **Category + tags:** [FashionCLIP](https://huggingface.co/patrickjohncyh/fashion-clip) (`patrickjohncyh/fashion-clip`, **MIT**) — a CLIP ViT-B/32 fine-tuned on ~800K fashion products, used zero-shot. Base checkpoint [`laion/CLIP-ViT-B-32-laion2B-s34B-b79K`](https://huggingface.co/laion/CLIP-ViT-B-32-laion2B-s34B-b79K) (**MIT**). Downloaded once from Hugging Face on first run (~600 MB).
- **Colors:** no ML — k-means (scikit-learn) over the garment's non-transparent pixels, mapped to the nearest named color.
- **Seasons:** a soft category heuristic (low confidence).

Fields below their confidence threshold are omitted, so weak guesses are never pushed. The retired `Hat` category is not classified, and hairstyle presets are not garments and are never auto-tagged.

### Running the service

Use a virtual environment (required on PEP 668 systems such as Arch and recent Debian/Ubuntu). Install `torch` separately for your CUDA build (GPU is used automatically when available, otherwise CPU), then the rest of the dependencies:

```bash
python -m venv .venv-autotag && source .venv-autotag/bin/activate
pip install torch --index-url https://download.pytorch.org/whl/cu124   # match your CUDA; omit the index for a CPU build
pip install -r tools/requirements-autotag.txt
python tools/autotag_server.py --host 127.0.0.1 --port 7100
```

On Arch you can instead reuse a system-installed `python-pytorch-cuda` by creating the venv with `--system-site-packages`.

Then run the API with the default `AutoTagging__Provider=Auto`. If the service is not running, `Auto` finds its `/health` endpoint unreachable and disables prefill: uploads behave exactly as before with manual metadata entry, and no errors surface. Set `AutoTagging__Provider=Disabled` to turn prefill off explicitly, or `HttpServer` to always target the service. Backend tests mock the tagger, so the model is not required to run them.

## Optional Try-On Providers

The backend uses the mock try-on provider by default. The mock returns deterministic demo output and does not spend real provider credits. Paid, uncached provider work runs from the hosted background worker after `POST /api/outfits/{outfitId}/try-on` has returned an accepted job resource.

The FASHN scaffold is enabled by setting `TryOn__Provider=Fashn` and `Fashn__ApiKey`; the publicly-hosted dev stack wires these plus the other `Fashn__*` options from `.env` (all keys are in the Configuration table).

The Builder asks the API for a try-on cost estimate before generation. The API classifies `Top`, `Bottom`, `Dress`, and `Outerwear` as body try-on items; `Shoes`, `Bag`, and `Accessory` are visual-only and are excluded from normal AI modes. Hairstyle presets are not garments and never reach try-on providers. AI try-on estimates and starts are blocked until the current account has `gender` set.

Try-on modes:

- `ClothesOnlyPreview`: free, no body reference required, no AI provider call.
- `SingleGarmentTryOn`: FASHN `tryon-max`, quality, one provider run, exactly one body try-on item. Credits per run follow `FASHN_RESOLUTION` (`1k` = 2, `4k` = 5). The full-resolution garment cutout is sent to FASHN.
- `SequentialOutfitTryOn`: FASHN `tryon-max`, quality, one provider run per body try-on item. Credits per run follow `FASHN_RESOLUTION` (`1k` = 2, `4k` = 5).
- `ExperimentalCompositeTryOn`: one composed garment reference image, 1 credit, explicitly premium and allowed to include visual-only items.

Generation requests must echo the server-estimated mode, credits, and cache key. The backend recomputes the estimate and rejects stale or mismatched confirmations. Successful generated jobs are cached by body reference, included garment IDs, provider, mode, and provider settings, so repeat requests can reuse existing outputs without calling AI.

Local/dev provider adapters (`LocalVton`, `LocalCatVton`, `SelfHostedCatVton`, `CompositeFashn`, `GeneralImageEdit`, `Replicate`, and `Fal`) target an HTTP service through their `TryOn__...__BaseUrl`/`Endpoint` settings and share the same JSON adapter shape. API-key-backed adapters require their matching `TryOn__...__ApiKey` setting (see the Configuration table).

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
| `PATCH` | `/account/profile` | Update account `username` and `gender`. |
| `POST` | `/account/avatar` | Upload and persist the current account avatar through private signed object storage. |
| `DELETE` | `/account` | Delete the current account and clear auth cookies. |
| `GET` | `/account/entitlements` | The account's plan limits, current usage, allowed AI try-on modes, resolution cap, and AI-credit balance. |
| `GET` | `/admin/stats` | Admin-only totals for users, garments, outfits, and try-on jobs. |
| `GET` | `/admin/users?q=&role=&offset=&limit=` | Admin-only paged/searchable user list with per-user data counts. |
| `GET` | `/admin/users/{userId}` | Admin-only single user view. |
| `PUT` | `/admin/users/{userId}/role` | Admin-only role change. Pinned accounts and your own account are rejected with `409`. |
| `POST` | `/admin/users/{userId}/credits` | Admin-only AI-credit adjustment (appends an `AdminAdjustment` ledger row, returns the new balance). |
| `POST` | `/admin/users/{userId}/sessions/revoke` | Admin-only revocation of all sessions of a user. |
| `POST` | `/admin/users/{userId}/purge-ai-outputs` | Admin-only purge of a user's AI outputs. |
| `GET` | `/admin/users/{userId}/export` | Admin-only export of a user's data with a sanitized account record. |
| `DELETE` | `/admin/users/{userId}` | Admin-only account deletion (pinned accounts and self are rejected with `409`). |
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
| `DELETE` | `/outfits/{outfitId}/try-on-preview` | Remove the active try-on preview from a saved outfit, deleting the matching stored output when available. |
| `POST` | `/outfits/{outfitId}/try-on/estimate` | Estimate mode availability, credits, included/excluded garments, cache key, and cache-hit status before generation. |
| `POST` | `/outfits/{outfitId}/try-on` | Confirm the server estimate and return `202 Accepted` with a try-on job. Free and cached jobs can already be `Succeeded`; paid uncached jobs are queued. |
| `GET` | `/try-on-jobs/{jobId}` | Read try-on job status/result. |
| `DELETE` | `/try-on-jobs/{jobId}/output` | Mark one try-on output deleted, remove the stored output URL from the job, and clear the linked outfit preview when that output is active. |
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

Playwright e2e smoke:

```powershell
cd outfit_planner_front
npm run test:e2e
```

Docker smoke checks:

```powershell
docker compose -f docker-compose.dev.yml up -d --build
curl.exe -k https://localhost:5001/api/health
```

## Current Boundaries

- Google and Apple auth require provider credentials. Email/password auth works without external secrets.
- The paywall (tier caps, AI-credit metering, mode/resolution gating, priority queue) is enforced and Stripe billing is implemented per [`PAYWALL_MODEL.md`](PAYWALL_MODEL.md), but no Stripe credentials ship with the repo: until `Stripe__SecretKey`/`Stripe__WebhookSecret`/price ids are configured, billing reads as disabled, `Free ↔ Premium` transitions happen manually through the admin panel, and the tier numbers/prices are provisional placeholders.
- Password registration requires at least 8 characters, at least one letter, and at least one digit.
- Uploaded files default to local object storage; S3-compatible MinIO can be enabled with object storage configuration.
- PostgreSQL schema changes are applied through DbUp migrations at startup.
- Garment categories are Top, Bottom, Dress, Outerwear, Shoes, Bag, and Accessory. Head wear is covered by global hairstyle presets (`GET /api/hairstyles`), not garments.
- The mock try-on provider is the default. FASHN, composite FASHN, GeneralImageEdit, Replicate, Fal, and local VTON/CatVTON providers require explicit environment configuration.

## Troubleshooting

- If the frontend shows network failures, confirm the API is available at `https://localhost:5001/api/health`.
- If running frontend dev against a non-default API port, set `VITE_DEV_API_TARGET`.
- If PostgreSQL connection fails from local Windows development, confirm the compose database is reachable on host port `5433`, not `5432`.
- If photo upload fails through Docker production frontend, check nginx `client_max_body_size` and the API upload diagnostics in logs.
- If FASHN returns no result for multi-garment outfits, use `SingleGarmentTryOn` to isolate one garment or `SequentialOutfitTryOn` for one provider run per body try-on item.
