# Try-On Cost Estimator Design

Date: 2026-06-21

## Purpose

This design adds explicit try-on cost estimation, item classification, mode selection, backend-enforced user confirmation, provider boundaries, and job caching before AI generation. The goal is to make paid try-on behavior predictable: users see what will be sent to AI, how many credits it costs, and which outfit items are excluded before generation starts.

The design keeps the current queued try-on pipeline. `POST /api/outfits/{outfitId}/try-on` must still create or return a job quickly, and non-free provider work must still happen from the background worker.

## Current Context

The backend already has a clean application-level try-on service:

- `TryOnService.StartAsync` validates consent, creates a `Queued` `TryOnJob`, records provider metadata, and enqueues the job.
- `TryOnService.ProcessQueuedJobAsync` loads the outfit and calls the configured `ITryOnProvider`.
- `ITryOnProvider` is currently selected once at startup and receives the full outfit plus `TryOnOptions`.
- FASHN currently supports a single garment or sequential multi-garment flow.
- Try-on jobs are persisted in in-memory and PostgreSQL stores.

The frontend Builder currently starts generation directly with `consentAccepted: true` and a `Sequential flow` toggle. It does not show a cost estimate or require a separate confirmation step.

## Scope

In scope:

- Add a `TryOnCostEstimator` in the Application layer.
- Classify outfit items into body try-on items and visual-only items.
- Add explicit try-on modes and server-side cost estimates.
- Add a user-facing confirmation flow before generation.
- Enforce the confirmed cost on the backend.
- Add provider abstractions that can support FASHN, composite FASHN, self-hosted CatVTON, and general image-edit providers.
- Cache generated jobs by body reference, garment IDs, provider, mode, and settings.
- Prevent visual-only items from reaching paid AI providers unless the user explicitly chooses premium composite mode.
- Update API contracts, frontend client/types, tests, README, and saved context.

Out of scope:

- Adding billing account balances or payment processing.
- Implementing a full server-side garment compositing renderer beyond what is needed for the provider abstraction.
- Charging or deducting real credits.
- Reworking wardrobe categories or body zones.
- Refactoring unrelated Builder UI or try-on queue infrastructure.

## Item Classification

`TryOnCostEstimator` classifies `Outfit.Items` by `GarmentCategory`.

Body try-on items:

- `Top`
- `Bottom`
- `Dress`
- `Outerwear`

Visual-only items:

- `Shoes`
- `Bag`
- `Accessory`
- `Hat`

Only body try-on items are eligible for normal paid virtual try-on. Visual-only items are shown in the estimate as excluded from normal AI generation. They are sent to AI only when the user explicitly selects `ExperimentalCompositeTryOn`.

If an outfit contains only visual-only items, normal AI modes are unavailable and the estimator must recommend `ClothesOnlyPreview`.

## Try-On Modes

`TryOnMode` must be a shared API enum with these values:

- `ClothesOnlyPreview`
- `SingleGarmentTryOn`
- `SequentialOutfitTryOn`
- `ExperimentalCompositeTryOn`

### ClothesOnlyPreview

Cost: 0 credits.

This mode is free and does not call any AI provider. It represents the existing clothes-only preview behavior. If the backend receives this mode, it must not enqueue provider work. The frontend continues to render the local clothes stack for this mode.

### SingleGarmentTryOn

Cost: 1 credit.

This mode uses FASHN `tryon-v1.6` for exactly one body try-on item. Visual-only items are excluded. If the outfit has more than one body try-on item, the estimator marks this mode unavailable and explains that sequential or composite mode is required.

### SequentialOutfitTryOn

Cost: `N` credits, where `N` is the number of body try-on items.

This mode uses FASHN `tryon-v1.6` once per body try-on item. The worker applies body try-on garments one after another, using the previous output as the next model image. Visual-only items are excluded.

### ExperimentalCompositeTryOn

Cost: 1 credit.

This mode uses one composed garment reference image and one provider run. It is explicitly premium and experimental. It can include visual-only items because the user intentionally chose the mode that sends the composed outfit reference to AI.

The first implementation must route this to a composite provider abstraction and return a clear error when the concrete provider is not configured. It must not silently fall back to sequential generation because that changes both cost and privacy expectations.

## Cost Estimate Contract

The backend must expose this estimate endpoint before generation:

`POST /api/outfits/{outfitId}/try-on/estimate`

The request includes the selected mode, body reference photo identity when relevant, and generation settings. The response includes:

- `mode`
- `provider`
- `bodyTryOnItems`
- `visualOnlyItems`
- `includedGarmentIds`
- `excludedGarmentIds`
- `estimatedCredits`
- `isAvailable`
- `requiresAi`
- `requiresPremiumConfirmation`
- `cacheKey`
- `hasCachedResult`
- user-facing `summary` and `warnings`

The endpoint must use the same estimator and cache-key builder as generation. The frontend must not calculate paid credits itself.

## Backend-Enforced Confirmation

Generation requires a confirmation payload. The client must send the exact mode and estimated credits it showed to the user:

- `tryOnMode`
- `confirmedCredits`
- optional confirmed estimate/cache token or cache key
- `consentAccepted`
- `bodyReferencePhotoId` or `bodyReferencePhotoUrl`
- generation settings

`TryOnService.StartAsync` recomputes the estimate on the server and rejects the request if:

- consent is missing for an AI mode,
- the mode is unavailable for the outfit,
- `confirmedCredits` does not match the recomputed estimate,
- the confirmed mode does not match the recomputed mode,
- the request would send only visual-only items to AI in a non-premium mode,
- required body reference input is missing for an AI mode.

This makes the frontend confirmation user-facing and the backend confirmation authoritative.

## Provider Abstraction

The Application layer must keep depending on provider ports, not Infrastructure classes. The current provider interface will evolve from outfit-based input toward a mode-aware request object:

- user ID
- outfit ID
- try-on mode
- provider name
- body reference image URL
- included body try-on items
- included visual-only items when premium mode is selected
- generation settings

Provider adapters must expose stable names used in cache keys and job metadata.

Required concrete provider classes:

- `FashnTryOnProvider`: normal FASHN `tryon-v1.6` single/sequential body garment generation.
- `CompositeFashnTryOnProvider`: FASHN-compatible one-run composite reference generation.
- `SelfHostedCatVtonProvider`: future self-hosted CatVTON adapter.
- `GeneralImageEditTryOnProvider`: future general image-edit adapter.
- `MockTryOnProvider`: local development and tests.

Provider selection remains configuration-driven, but generation must be mode-aware. Unsupported modes must fail clearly before network calls when the selected provider cannot perform the requested mode.

## Caching

Generated jobs must be cached by:

- body reference identity,
- ordered garment IDs,
- provider name,
- try-on mode,
- generation settings.

The stable body reference identity must be `body:{BodyReferencePhotoId}` when a stored body photo ID is supplied and belongs to the current user. If no stored photo ID is available, the fallback identity is `url:{normalizedBodyReferencePhotoUrl}`.

The stable garment identity must use sorted garment IDs from the included generation set for the selected mode. For normal modes this means body try-on items only; for `ExperimentalCompositeTryOn` this includes both body try-on and visual-only items.

Settings must be serialized in a deterministic order and hashed. Settings include provider model name, provider mode, sequential flag when applicable, and any explicit generation options that can change the output.

On cache hit:

- Do not enqueue a provider job.
- Do not call any AI provider.
- Return a succeeded job or a cache-hit job record that points at the cached output.
- Preserve the current user boundary. Jobs from another user must not be used as cache hits.
- Do not return outputs from jobs marked deleted or purged.

PostgreSQL and in-memory storage need a query path for latest succeeded non-deleted job by user and cache key.

## Job Metadata

`TryOnJob` must carry enough metadata to audit how a job was produced:

- try-on mode,
- estimated or confirmed credits,
- cache key,
- whether it was served from cache,
- source cached job ID when applicable,
- provider settings hash,
- existing provider job ID/request ID fields.

Database migrations and the compatibility `database/schema.sql` snapshot must stay aligned.

## Frontend Flow

Builder must replace direct generation with a two-step flow:

1. Ask the API for a cost estimate for the selected outfit, body reference photo, mode, and settings.
2. Show a confirmation panel or dialog with credits, provider/mode, included body items, excluded visual-only items, cache-hit status, and warnings.
3. Start generation only after the user confirms.

The generation request must echo the server estimate values required by backend enforcement.

The old `Sequential flow` toggle must become part of explicit mode selection. Use these labels:

- `Clothes only`: free, local preview.
- `Single garment`: 1 credit.
- `Sequential outfit`: one credit per body garment.
- `Composite premium`: 1 credit, experimental, includes accessories.

The UI must make excluded items visible. When a user selects top, bottom, shoes, and bag with sequential mode, the estimate must say the top and bottom are included and shoes/bag are visual-only and not sent to AI.

## API And Frontend Contract Updates

Because API contracts change, update together:

- `outfit_planner_back/src/OutfitPlanner.Api/Contracts/ApiContracts.cs`
- route mapping in `Program.cs`
- generated OpenAPI artifacts via `npm run generate:api`
- `outfit_planner_front/src/api/client.ts`
- `outfit_planner_front/src/types.ts`
- backend tests
- frontend API and Builder tests
- README and saved project context

Generated OpenAPI schema files remain ignored and must not be committed.

## Error Handling

Estimator errors must be actionable:

- no outfit found,
- no selected garments,
- no body try-on items for paid modes,
- single mode selected for multiple body items,
- body reference missing for AI modes,
- provider does not support selected mode,
- confirmation mismatch.

The frontend must show these errors near the generation controls and keep the user in Builder. Confirmation mismatch must ask the user to refresh the estimate rather than retrying the stale generation request.

## Privacy And Safety

Body reference photos remain sensitive. This design does not re-enable public body reference photo serving. Generation must continue using signed `/api/storage/signed/...` URLs or configured object storage URLs.

Visual-only items must not be sent to AI unless `ExperimentalCompositeTryOn` is explicitly selected and confirmed. This rule is enforced by the estimator and by `TryOnService.StartAsync`, not only by the UI.

Cached outputs remain subject to existing privacy endpoints:

- `DELETE /api/try-on-jobs/{id}/output`
- `POST /api/privacy/purge-ai-outputs`
- `DELETE /api/account`

Deleted or purged outputs are not valid cache hits.

## Testing Strategy

Add tests before implementation for these behaviors:

- estimator classifies body and visual-only items correctly,
- clothes-only mode costs 0 and does not call providers,
- single garment mode costs 1 and rejects multiple body try-on items,
- sequential mode costs one credit per body try-on item and excludes visual-only items,
- premium composite mode costs 1 and can include visual-only items,
- backend generation rejects missing or mismatched confirmed credits,
- backend generation rejects visual-only AI usage in non-premium modes,
- cache hit returns a completed job without enqueueing or provider calls,
- provider adapters implement the provider port,
- FASHN receives only body try-on items for normal modes,
- Builder displays the estimate and requires user confirmation before generation,
- frontend start request sends mode and confirmed credits,
- OpenAPI-derived frontend types include the new contracts.

Final verification must include:

- backend console tests,
- backend build,
- frontend tests,
- frontend build.

## Documentation Updates

Update README and saved agent context because this is a meaningful behavior and API-contract change. The docs must describe:

- try-on cost estimation,
- explicit modes and credit behavior,
- visual-only item exclusion,
- premium composite behavior,
- cache behavior,
- provider configuration names.

## Acceptance Criteria

The feature is complete when:

- Try-on estimates are produced by the backend and shown before generation.
- Generation is blocked unless the client confirms the current backend-estimated cost.
- Normal AI modes include only `Top`, `Bottom`, `Dress`, and `Outerwear`.
- `Shoes`, `Bag`, `Accessory`, and `Hat` are not sent to AI unless premium composite mode is explicitly selected.
- Clothes-only preview is free and does not call AI.
- Single, sequential, and composite modes report the expected credit counts.
- Cache hits avoid provider calls and queue work.
- Provider abstractions support the named future providers without breaking existing mock and FASHN flows.
- API contracts, frontend client/types, tests, README, and saved context are updated together.
