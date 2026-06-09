# Outfit Planner Foundation First Design

Date: 2026-06-09

## Purpose

This design covers the first implementation slice for the frontend modernization request. The slice is intentionally foundational: it prepares routing, module boundaries, generated API contracts, PWA basics, and regression tests before adding the larger Wardrobe, Builder, Calendar, and Share UX feature set.

The product UX requests for edit/duplicate/archive/favorite workflows, richer planning, share options, camera/crop flows, and outfit history are deferred to the next slice unless they already exist and must be preserved during refactor.

## Current Context

The frontend currently keeps most app behavior in `outfit_planner_front/src/App.tsx`, which is about 1323 lines and contains shell layout, auth, route definitions, Wardrobe, Builder, Calendar, Share, and many shared UI components.

The backend already exposes many private CRUD endpoints that the future UX work can use:

- Garments: list/get/create/update/delete with filters, favorite/archive metadata, and upload endpoints.
- Outfits: list/get/create/update/delete with favorite/archive metadata and try-on generation.
- Schedule: plan/list/remove one outfit per day.
- Share: create/read/revoke links.
- Auth: cookie-backed session and CSRF-protected private routes.

The frontend currently has manual types in `src/types.ts` and a manual API wrapper in `src/api/client.ts`.

## Scope

In scope for this slice:

- Add a real route auth guard for private pages.
- Split `App.tsx` into app, route, feature, and shared UI modules.
- Preserve existing Wardrobe, Builder, Calendar, Auth, and Share behavior during the split.
- Fix the stale `activeOutfit` versus draft `selection` Builder risk.
- Add backend OpenAPI output and generated frontend client/types.
- Keep a stable frontend API wrapper for cookies, CSRF, uploads, and diagnostics.
- Add foundation-level PWA support.
- Add or split tests that protect the new structure and behavior.
- Update saved context after meaningful changes.

Out of scope for this slice:

- Implementing the full Wardrobe UX feature list.
- Implementing the full Builder UX feature list.
- Implementing the full Calendar planner feature list.
- Implementing full Share link privacy/expiry/Open Graph management.
- Adding backend schema/storage changes for recurring plans, share expiry, try-on history listing, packing trips, or wore confirmations.
- Replacing the app's visual system.

## Architecture

`src/App.tsx` should stop being the application monolith. The implementation should introduce these boundaries:

- `src/app/App.tsx`: route tree and top-level app composition.
- `src/app/AppShell.tsx`: layout, navigation, theme handling, auth sidebar actions, and responsive navigation.
- `src/app/RequireAuth.tsx`: private route guard.
- `src/routes/WardrobePage.tsx`: wardrobe route component.
- `src/routes/BuilderPage.tsx`: builder route component.
- `src/routes/CalendarPage.tsx`: calendar route component.
- `src/routes/AuthPage.tsx`: sign-in/register route component.
- `src/routes/SharePage.tsx`: public shared outfit route component.
- `src/features/auth/`: auth hooks, return URL helpers, auth feature components.
- `src/features/wardrobe/`: wardrobe feature helpers and components.
- `src/features/builder/`: builder feature helpers and components.
- `src/features/calendar/`: calendar feature helpers and components.
- `src/features/tryon/`: try-on feature helpers and components.
- `src/shared/ui/`: reusable UI such as page headers, panel titles, file picker, skeletons, empty states, date picker, category controls, and preview empty-state components.

The existing `src/App.tsx` may remain as a compatibility re-export to avoid churn in test imports, but `main.tsx` should import the app entry from `src/app/App`.

Private routes are:

- `/`
- `/wardrobe`
- `/builder`
- `/calendar`

Public routes are:

- `/signin`
- `/register`
- `/share/:token`

## Auth Guard

`RequireAuth` should use the shared auth session query:

- If the session query is loading, render a shell-compatible skeleton.
- If the user is not authenticated, redirect to `/signin?returnUrl=<current-path-and-search>`.
- If the user is authenticated, render the protected route.

`AuthPage` should read `returnUrl`, validate that it is an internal app URL, and navigate there after successful login or registration. External auth start URLs should also use the same safe return URL.

API 401s can still be handled by the API wrapper, but private pages should not rely on first private API failure as the normal unauthenticated flow.

## API And Type Generation

The backend should expose an OpenAPI document without changing existing route behavior. JSON enum values must remain string values.

The frontend should add a repeatable generation workflow with these steps:

- Generate OpenAPI JSON from the backend.
- Generate TypeScript client/types into `outfit_planner_front/src/api/generated/`.
- Add npm scripts for generation and verification.

The generated code should not replace the app-facing API wrapper in the first slice. `src/api/client.ts` should remain the stable wrapper for:

- Same-origin `/api` convention.
- `credentials: 'include'`.
- CSRF header injection for mutating requests.
- Multipart upload behavior.
- Upload/network diagnostics.
- App-friendly method names.

Manual `src/types.ts` should shrink toward frontend-only types. Backend DTO shapes should be re-exported or derived from generated schemas where practical.

## Builder Draft State

Builder currently keeps `activeOutfit` separately from `selection`. This can leave the UI targeting a saved outfit that no longer matches the visible draft after the user changes a selected garment.

This slice should make the behavior explicit. Either:

- Clear `activeOutfit` whenever `selection` changes after picking or saving an outfit, or
- Separate a `draftSelection` from a saved `activeOutfit` and gate Share/try-on/save actions against the correct target.

The preferred implementation is clearing or invalidating `activeOutfit` on draft selection changes because it is smaller and matches the current UI model.

## PWA Foundation

This slice should make the app installable and prepare for mobile/offline work without implementing the full offline wardrobe feature.

The foundation includes:

- Web app manifest linked from `index.html`.
- App name, short name, theme color, display mode, and icon metadata.
- Service worker registration guarded for browser runtime and production suitability.
- Static shell caching for built assets.
- A safe offline fallback for navigation or a minimal offline page.
- Responsive bottom navigation in `AppShell` if it can be added without redesigning each route.

Offline wardrobe data caching, photo upload queueing, crop UI, and swipe outfit browsing are deferred.

## Existing UX Preservation

During the split, these current behaviors must keep working:

- Wardrobe can upload garment photos, categorize garments, show columns, and delete garments.
- Builder can select garments by category, quick-add missing slot garments, upload/delete body reference photos, save outfits, start mock try-on, show try-on status, and share the active saved outfit.
- Calendar can show the clay month picker and plan an outfit for a selected date.
- Auth can sign in/register and show provider buttons based on provider metadata.
- Share page can load a public shared outfit by token.

If existing tests encode these behaviors, they should be moved or kept passing rather than replaced by weaker assertions.

## Error Handling

The route guard should show a loading skeleton during the session request and redirect only after the unauthenticated state is known.

The safe return URL helper should reject external URLs and malformed paths, falling back to `/builder`.

Generated API/client integration should preserve current error messages where practical, especially upload diagnostics and API trace IDs.

Service worker registration should fail quietly with a console diagnostic rather than blocking app render.

## Testing Strategy

Add or update tests before or during implementation so behavior changes are covered.

Required test coverage:

- Auth guard renders loading skeleton while session is loading.
- Auth guard redirects unauthenticated private route access to `/signin?returnUrl=...`.
- Auth guard renders private content for an authenticated session.
- Auth page navigates to a safe return URL after login/register.
- Unsafe return URLs fall back to `/builder`.
- Wardrobe page core flow still renders and upload/delete behavior is preserved.
- Builder page core flow still renders and quick upload/body photo/try-on controls are preserved.
- Builder clears or invalidates active saved outfit when selection changes.
- Calendar page still uses the custom clay date picker.
- Share page still renders public shared outfit data.
- Upload validation tests remain in place.
- Generated API types/client are consumed by TypeScript build.
- PWA manifest and service worker registration have smoke coverage.

Final verification after implementation must include:

- `cd outfit_planner_front; npm test`
- `cd outfit_planner_front; npm run build`
- Backend OpenAPI generation or backend build command, depending on the chosen generation workflow.
- `dotnet build outfit_planner_back\src\OutfitPlanner.Api\OutfitPlanner.Api.csproj`
- Playwright e2e for register to share flow if the dev stack can be stood up in the environment. If it cannot run, report the exact blocker and the command attempted.

## Documentation Updates

After implementation, update `README.md` and/or `agents.md` briefly because this is a meaningful architecture and workflow change.

Likely updates:

- Repository layout should mention `src/app`, `src/routes`, `src/features`, and `src/shared/ui`.
- Frontend commands should mention OpenAPI client generation.
- PWA support should be described at feature level.
- Any stale README boundary that says only `Top` and `Bottom` are supported should be corrected.

## Acceptance Criteria

The slice is complete when:

- Private routes use `RequireAuth` and no longer rely on page-level 401s as the normal unauthenticated user flow.
- `App.tsx` is split into the agreed module structure.
- Existing page behavior remains covered and passing.
- Builder stale `activeOutfit` behavior is fixed and tested.
- Backend OpenAPI output and frontend generation workflow exist.
- Frontend consumes generated API types where practical without losing wrapper behavior.
- PWA foundation is present and smoke-tested.
- Relevant frontend/backend verification commands have been run and reported.
- Saved project context has been updated.
