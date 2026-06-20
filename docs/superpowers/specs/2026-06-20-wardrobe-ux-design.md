# Wardrobe UX Design

Date: 2026-06-20

## Purpose

This design covers the frontend-first Wardrobe UX slice. It implements the requested wardrobe management workflows without adding backend contracts unless implementation uncovers an existing API bug.

The selected structure is **Command center with upload rail**: a central wardrobe catalog with search, filters, tabs, and photo cards, plus a right-side rail that switches between add, edit, and upload queue states.

## Current Context

The current Wardrobe page is small and functional. It supports loading garments, single-photo upload, category selection, preview, simple columns, and delete.

The existing frontend API wrapper already exposes the needed operations:

- `listGarments(filters)`
- `createGarment(input)`
- `updateGarment(garmentId, input)`
- `deleteGarment(garmentId)`
- `uploadGarmentPhoto(file)`

The backend already supports garment metadata used by this slice: category, tags, primary color, season, favorite, archived, and other optional metadata fields. Therefore, this design keeps the slice frontend-first.

## Visual System Override

The Wardrobe redesign must follow the new editorial fashion UI shown in the user's dark and light reference screenshots. It must not extend the old claymorphism surface language.

Legacy claymorphism is still present in the current codebase, but this Wardrobe slice should migrate the visible wardrobe surface toward the new system:

- Editorial fashion/product UI, not soft clay dashboard.
- Left navigation, editorial intro, central catalog, right rail.
- Warm paper light theme and warm ink dark theme.
- Hairline borders, flat panels, restrained shadows.
- Serif display headings with italic crimson emphasis.
- Compact sans labels and form controls.
- Crimson accent for active navigation, selected tabs, important status, and primary actions.
- Primary buttons use a tactile crimson press-button treatment.
- Secondary buttons are quiet outlined controls.
- Product cards are clean fashion tiles with real garment imagery as the visual focus.
- No lavender canvas, animated blobs, convex gradient buttons, recessed clay inputs, or large rounded clay panels.
- Theme toggle should map to the new light and dark editorial palettes shown in the references.

Reference anchors:

- User-provided dark screenshot, based on Obra Studio.
- User-provided light screenshot, based on Crimson Plinth.
- Obra Studio public description: https://uiverse.io/design/systems/obra-studio
- Crimson Plinth public description: https://uiverse.io/design/systems/crimson-plinth

## Scope

In scope:

- Edit garment.
- Duplicate garment.
- Archive garment.
- Favorite garment.
- Bulk upload.
- Drag-and-drop upload.
- Mobile camera capture.
- Filters by category, color, season, and tags.
- Search by name and tags.
- Empty states with examples.
- Auto-tag suggestions after upload.
- Advisory "needs better photo" warning.
- Clean photo checklist before upload submit: front view, good lighting, no background clutter.
- Tests for the new Wardrobe behaviors.
- Minimal shared shell, navigation, and token updates needed for Wardrobe to sit inside the new editorial frame.

Out of scope:

- Backend bulk upload endpoint.
- Backend auto-tagging or AI tagging.
- Server-side computer vision quality scoring.
- Photo crop UI, unless already available through existing reusable code.
- Full app-wide visual redesign of Builder, Calendar, Auth, and Share in this slice.
- Reworking Builder, Calendar, Auth, and Share content screens beyond any shell/theme changes they inherit.
- API contract changes, unless implementation finds an existing API defect.

## Architecture

`src/routes/WardrobePage.tsx` remains the route entry, but behavior should move into focused feature modules under `src/features/wardrobe/`.

Recommended components and helpers:

- `WardrobeFilters`: search, category, color, season, tags, favorite/archive toggles, sort.
- `WardrobeUploadPanel`: add/edit rail shell, drag-and-drop target, camera input, clean photo checklist.
- `UploadQueue`: one row per selected photo, validation status, suggested tags, editable fields, per-row submit state.
- `GarmentCard`: photo tile with favorite, archive, edit, duplicate, and delete actions.
- `GarmentEditor`: right-rail edit state for the selected garment.
- `wardrobeSuggestions.ts`: local tag suggestions and photo warning heuristics.
- `wardrobeMutations.ts`: shared TanStack Query mutations for create, update, delete, duplicate, archive, favorite, and queued upload.

Shared UI can be added under `src/shared/ui/` only if it is reusable and follows the new editorial system. Existing clay-specific components should not be expanded for this redesigned surface unless they are purely structural.

Because Wardrobe is rendered inside the shared private app shell, implementation may update `src/app/AppShell.tsx` and shared style tokens enough to provide the new left navigation and theme frame. That shell change may affect other private routes visually, but their page-specific UX redesign remains out of scope for this slice.

## Data Flow

The garment list query should include the active filter object in the query key, then call `listGarments(filters)`.

Default filters:

- `archived: false`
- `sort: recent`

User-controlled filters:

- `category`
- `color`
- `season`
- `q`
- `favorite`
- `archived`
- `sort`
- local tag filter if the backend does not directly support tag-specific query parameters

Edit:

- Open selected garment in the right rail.
- PATCH changed fields through `updateGarment`.
- Invalidate `['garments']` queries after success.

Favorite:

- Toggle `isFavorite` with `updateGarment`.
- Use optimistic UI only if tests cover rollback, otherwise use pending state.

Archive:

- Toggle `isArchived` with `updateGarment`.
- Default list removes archived garments after success.
- Archived garments can be shown through the archived filter.

Duplicate:

- Create a garment using existing image URLs and metadata.
- New name format: `<original name> copy`.
- Preserve category, tags, color, season, and other safe metadata.
- Do not copy `lastWornAt`.
- Default the duplicate to `isFavorite: false` and `isArchived: false`.

Bulk upload:

- Selecting or dropping files creates upload queue rows.
- Each row is validated locally.
- Valid rows can be uploaded one by one or with a "Submit all" action.
- Each valid row calls `uploadGarmentPhoto(file)` and then `createGarment(input)`.
- Failed rows remain in the queue with an error and can be retried.

## Upload UX

The add/upload rail always shows the clean photo checklist before submit:

- Front view.
- Good lighting.
- No background clutter.

The checklist is informational and visible before the user commits an upload. It should not become a blocking multi-step wizard in this slice.

Inputs:

- Drag-and-drop target.
- Multi-file file input.
- Mobile camera capture input, using `accept="image/*"` and `capture="environment"` where supported.
- Editable name.
- Category selector.
- Tags input or chips.
- Primary color.
- Season chips.

Auto-tag suggestions:

- Derive suggestions from filename tokens.
- Include selected category.
- Include selected color and season.
- Reuse tags already present on similar garments where practical.
- Suggestions are chips the user can accept or ignore.

Photo warning heuristics:

- Very small file size.
- Very small image dimensions, if dimensions can be read in browser.
- Extreme aspect ratio.
- Generic filename such as `image`, `photo`, or camera timestamp.
- Validation or preview problems.

Warnings are advisory. Blocking validation remains limited to existing upload validation such as file type and size.

## Layout Strategy

Desktop:

- Use the editorial app frame from the reference screenshots.
- Left nav stays narrow and persistent.
- Main catalog area owns the visual focus.
- Right rail is fixed-width and task-oriented.
- Search and category filters sit above the catalog.
- Category tabs sit directly above the garment grid.
- Garment count is visible near tabs or controls.

Mobile:

- Keep the existing bottom navigation concept if it is already active.
- Stack the right rail below the filters and above or below the catalog depending on upload/edit mode.
- Keep tap targets at least 44px.
- Avoid text overflow in card actions and upload queue rows.

## Key States

Loading:

- Use skeleton tiles and a skeleton right rail.
- Avoid centered spinners.

Empty wardrobe:

- Show example upload prompts for a usable closet: front-view shirt, jeans, shoes, and one outer layer.
- Keep the upload rail visible.

Filtered empty:

- Explain that no garments match current filters.
- Provide a clear filter reset action.

Upload queue empty:

- Show the clean photo checklist and drop target.

Upload queue with files:

- Show one editable row per file.
- Surface validation errors and advisory photo warnings per row.
- Show accepted suggested tags per row.

Edit selected:

- Right rail title changes to edit mode.
- Save and cancel are explicit.
- Dirty state is visible through enabled save button and pending state.

Mutation error:

- Show local error near the action that failed.
- Preserve queued files and edited form state.

Archived view:

- Archived cards are visually subdued.
- User can restore by toggling archive off.

## Interaction Model

Primary user path:

1. User opens Wardrobe.
2. User sees searchable, filterable catalog.
3. User opens the upload rail or drops photos.
4. User reviews the clean photo checklist.
5. User accepts tag suggestions and metadata.
6. User uploads all valid rows.
7. New garments appear in the catalog.

Card actions:

- Star toggles favorite.
- Archive moves item out of default catalog.
- Edit opens right rail.
- Duplicate creates a visible copy.
- Delete remains available but is visually secondary to archive.

Keyboard and accessibility:

- Search and filters must be labeled.
- Card icon buttons need garment-specific accessible names.
- Drop target must also expose a normal file input.
- Upload queue rows need readable status text.

## Testing Strategy

Add or update tests before implementation where practical.

Required frontend tests:

- Wardrobe renders the new search/filter controls.
- Wardrobe calls `listGarments` with active filters.
- Empty wardrobe shows example garments and upload guidance.
- Filtered empty state offers reset.
- Favorite action sends `PATCH` with `isFavorite`.
- Archive action sends `PATCH` with `isArchived`.
- Edit rail loads a garment and saves changed fields.
- Duplicate sends `POST` with copied metadata and a copy name.
- Bulk upload creates multiple queue rows.
- Drag-and-drop adds files to queue.
- Camera capture input exists for mobile upload.
- Clean photo checklist is visible in upload mode before submit.
- Auto-tag suggestions appear from filename/category/color/season.
- Needs better photo warning appears for low-quality heuristic cases.
- Existing delete behavior still works.

Verification after implementation:

- `cd outfit_planner_front; npm test -- src/routes/WardrobePage.test.tsx`
- `cd outfit_planner_front; npm test`
- `cd outfit_planner_front; npm run build`
- Backend tests/build only if backend files change.

## Acceptance Criteria

The Wardrobe UX slice is complete when:

- The visible Wardrobe surface uses the new editorial Obra/Crimson-inspired visual language.
- Old claymorphism palette, blobs, panels, and button styles are not used for the redesigned Wardrobe surface.
- Wardrobe supports edit, duplicate, archive, favorite, bulk upload, drag-and-drop upload, mobile camera capture, filters, search, empty examples, auto-tag suggestions, and photo quality warning.
- The clean photo checklist is visible before upload submit.
- Archived garments are hidden by default and recoverable through filters.
- Duplicate, archive, favorite, edit, and delete use existing frontend API wrapper functions.
- No generated files are committed.
- Tests and build pass, or any blocker is reported with the exact command attempted.
