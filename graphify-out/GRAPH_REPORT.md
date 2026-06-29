# Graph Report - outfit_planner  (2026-06-29)

## Corpus Check
- 161 files · ~131,503 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 1525 nodes · 2875 edges · 101 communities (76 shown, 25 thin omitted)
- Extraction: 99% EXTRACTED · 1% INFERRED · 0% AMBIGUOUS · INFERRED: 34 edges (avg confidence: 0.83)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `74494611`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- [[_COMMUNITY_PostgreSQL Outfit Store|PostgreSQL Outfit Store]]
- [[_COMMUNITY_Auth Service & Security|Auth Service & Security]]
- [[_COMMUNITY_File-Backed Outfit Store|File-Backed Outfit Store]]
- [[_COMMUNITY_Frontend API Client|Frontend API Client]]
- [[_COMMUNITY_Schedule Service & Commands|Schedule Service & Commands]]
- [[_COMMUNITY_Background Removal Providers|Background Removal Providers]]
- [[_COMMUNITY_S3 Object Storage|S3 Object Storage]]
- [[_COMMUNITY_Frontend Package Dependencies|Frontend Package Dependencies]]
- [[_COMMUNITY_In-Memory Outfit Store|In-Memory Outfit Store]]
- [[_COMMUNITY_Try-On Background Worker|Try-On Background Worker]]
- [[_COMMUNITY_Test HTTP Recording Harness|Test HTTP Recording Harness]]
- [[_COMMUNITY_Try-On Cost Estimator|Try-On Cost Estimator]]
- [[_COMMUNITY_Outfit & Try-On Repositories|Outfit & Try-On Repositories]]
- [[_COMMUNITY_Auth Route Guard|Auth Route Guard]]
- [[_COMMUNITY_Frontend Response Types|Frontend Response Types]]
- [[_COMMUNITY_Outfit Utilities & Naming|Outfit Utilities & Naming]]
- [[_COMMUNITY_User Account Repository|User Account Repository]]
- [[_COMMUNITY_Wardrobe Filter Controls|Wardrobe Filter Controls]]
- [[_COMMUNITY_Backend NuGet Dependencies|Backend NuGet Dependencies]]
- [[_COMMUNITY_API Request Contracts|API Request Contracts]]
- [[_COMMUNITY_Try-On Output Storage|Try-On Output Storage]]
- [[_COMMUNITY_Local Photo Storage|Local Photo Storage]]
- [[_COMMUNITY_HTTP Try-On Providers|HTTP Try-On Providers]]
- [[_COMMUNITY_Garment Repository & Search|Garment Repository & Search]]
- [[_COMMUNITY_Photo Upload Service|Photo Upload Service]]
- [[_COMMUNITY_Outfit Service|Outfit Service]]
- [[_COMMUNITY_Image Processor|Image Processor]]
- [[_COMMUNITY_Upload Queue UI|Upload Queue UI]]
- [[_COMMUNITY_App Shell & Account UI|App Shell & Account UI]]
- [[_COMMUNITY_Wardrobe Upload Logic|Wardrobe Upload Logic]]
- [[_COMMUNITY_Schedule & Share Repositories|Schedule & Share Repositories]]
- [[_COMMUNITY_TS App tsconfig|TS App tsconfig]]
- [[_COMMUNITY_FASHN Try-On Provider|FASHN Try-On Provider]]
- [[_COMMUNITY_Garment Card & Editor UI|Garment Card & Editor UI]]
- [[_COMMUNITY_Shared Outfit Page|Shared Outfit Page]]
- [[_COMMUNITY_Stored Photo URL Refresher|Stored Photo URL Refresher]]
- [[_COMMUNITY_Storage & Deploy Concepts|Storage & Deploy Concepts]]
- [[_COMMUNITY_Photo Storage Abstractions|Photo Storage Abstractions]]
- [[_COMMUNITY_Community 38|Community 38]]
- [[_COMMUNITY_Calendar UI & Utils|Calendar UI & Utils]]
- [[_COMMUNITY_User Account Persistence|User Account Persistence]]
- [[_COMMUNITY_OpenAPI Client Generator|OpenAPI Client Generator]]
- [[_COMMUNITY_Wardrobe Dark Theme (Final)|Wardrobe Dark Theme (Final)]]
- [[_COMMUNITY_Wardrobe Dark Theme (After)|Wardrobe Dark Theme (After)]]
- [[_COMMUNITY_Try-On Cost & Admin Concepts|Try-On Cost & Admin Concepts]]
- [[_COMMUNITY_Wardrobe Sidebar Dark Theme|Wardrobe Sidebar Dark Theme]]
- [[_COMMUNITY_Garment Category Controls|Garment Category Controls]]
- [[_COMMUNITY_Wardrobe Button Contrast UI|Wardrobe Button Contrast UI]]
- [[_COMMUNITY_API Launch Settings|API Launch Settings]]
- [[_COMMUNITY_External Login Persistence|External Login Persistence]]
- [[_COMMUNITY_TS Node tsconfig|TS Node tsconfig]]
- [[_COMMUNITY_Google OAuth Handler|Google OAuth Handler]]
- [[_COMMUNITY_Community 52|Community 52]]
- [[_COMMUNITY_Community 53|Community 53]]
- [[_COMMUNITY_Auth Foundation Concepts|Auth Foundation Concepts]]
- [[_COMMUNITY_Import Outfit Store Script|Import Outfit Store Script]]
- [[_COMMUNITY_Outfit Composition Rules|Outfit Composition Rules]]
- [[_COMMUNITY_Community 57|Community 57]]
- [[_COMMUNITY_PWA & README Concepts|PWA & README Concepts]]
- [[_COMMUNITY_Community 59|Community 59]]
- [[_COMMUNITY_Editorial Visual System|Editorial Visual System]]
- [[_COMMUNITY_Postgres Connection Probe|Postgres Connection Probe]]
- [[_COMMUNITY_Community 62|Community 62]]
- [[_COMMUNITY_Image File Validation|Image File Validation]]
- [[_COMMUNITY_rembg Server Script|rembg Server Script]]
- [[_COMMUNITY_Backend Architecture Concepts|Backend Architecture Concepts]]
- [[_COMMUNITY_Domain Rules Concepts|Domain Rules Concepts]]
- [[_COMMUNITY_Postgres Migration Runner|Postgres Migration Runner]]
- [[_COMMUNITY_Wardrobe Page Tests|Wardrobe Page Tests]]
- [[_COMMUNITY_Input Guard|Input Guard]]
- [[_COMMUNITY_Design Token Tests|Design Token Tests]]
- [[_COMMUNITY_App Icon Branding|App Icon Branding]]
- [[_COMMUNITY_Root tsconfig|Root tsconfig]]
- [[_COMMUNITY_Program Entry|Program Entry]]
- [[_COMMUNITY_Try-On Status Enum|Try-On Status Enum]]
- [[_COMMUNITY_E2E RegisterUpload Spec|E2E Register/Upload Spec]]
- [[_COMMUNITY_Service Worker Shell|Service Worker Shell]]
- [[_COMMUNITY_Privacy Endpoints|Privacy Endpoints]]
- [[_COMMUNITY_Community 85|Community 85]]
- [[_COMMUNITY_Community 86|Community 86]]
- [[_COMMUNITY_Community 87|Community 87]]
- [[_COMMUNITY_Community 89|Community 89]]
- [[_COMMUNITY_Community 90|Community 90]]
- [[_COMMUNITY_Community 91|Community 91]]
- [[_COMMUNITY_Community 92|Community 92]]
- [[_COMMUNITY_Community 93|Community 93]]
- [[_COMMUNITY_Community 94|Community 94]]
- [[_COMMUNITY_Community 95|Community 95]]
- [[_COMMUNITY_Community 96|Community 96]]
- [[_COMMUNITY_Community 97|Community 97]]
- [[_COMMUNITY_Community 98|Community 98]]
- [[_COMMUNITY_Community 99|Community 99]]
- [[_COMMUNITY_Community 100|Community 100]]
- [[_COMMUNITY_Community 101|Community 101]]

## God Nodes (most connected - your core abstractions)
1. `PostgresOutfitStore` - 79 edges
2. `InMemoryOutfitStore` - 69 edges
3. `FileBackedOutfitStore` - 65 edges
4. `request()` - 38 edges
5. `Outfit` - 35 edges
6. `AuthService` - 32 edges
7. `GarmentItem` - 30 edges
8. `TryOnJob` - 30 edges
9. `TryOnService` - 29 edges
10. `LocalPhotoStorage` - 29 edges

## Surprising Connections (you probably didn't know these)
- `Local Signed Storage Public URL for FASHN` --semantically_similar_to--> `Same-Origin /api and /uploads Convention`  [INFERRED] [semantically similar]
  docs/deploy-plan.txt → AGENTS.md
- `Dev Docker Compose Stack` --conceptually_related_to--> `Tech Stack`  [INFERRED]
  docker-compose.dev.yml → README.md
- `OpenAPI Client Generation Workflow` --conceptually_related_to--> `API Overview`  [INFERRED]
  docs/superpowers/plans/2026-06-09-foundation-first-implementation.md → README.md
- `AdminService Use Cases` --conceptually_related_to--> `Backend Onion Projects (Domain/Application/Infrastructure/Api)`  [INFERRED]
  docs/superpowers/specs/2026-06-23-admin-foundation-design.md → AGENTS.md
- `Local Signed Storage Public URL for FASHN` --rationale_for--> `Signed Object URLs for Uploads`  [INFERRED]
  docs/deploy-plan.txt → AGENTS.md

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **Try-On Cost Estimation Pipeline** — docs_superpowers_specs_2026_06_21_tryon_cost_estimator, agents_tryon_modes, agents_tryon_classification, agents_tryon_cache, docs_superpowers_specs_2026_06_21_backend_confirmed_cost [EXTRACTED 1.00]
- **Frontend Foundation Slice** — docs_superpowers_plans_2026_06_09_openapi_generation, docs_superpowers_plans_2026_06_09_require_auth, docs_superpowers_plans_2026_06_09_app_shell_split, docs_superpowers_plans_2026_06_09_pwa_foundation [EXTRACTED 1.00]
- **Self-Hosted Production Deploy Stack** — docker_compose_prod, docs_deploy_plan_cloudflare_vps, docs_deploy_plan_local_storage_public_url, docker_compose_selfhost_override_rembg [INFERRED 0.85]

## Communities (101 total, 25 thin omitted)

### Community 0 - "PostgreSQL Outfit Store"
Cohesion: 0.13
Nodes (5): NpgsqlDataSource, DateOnly, DateTimeOffset, IReadOnlyList, PostgresOutfitStore

### Community 1 - "Auth Service & Security"
Cohesion: 0.06
Nodes (19): IAuthTokenService, IPasswordHasher, AuthenticatedSession, AuthResult, AuthService, AuthSessionInfo, DateTimeOffset, HashSet (+11 more)

### Community 2 - "File-Backed Outfit Store"
Cohesion: 0.08
Nodes (11): Action, DateOnly, DateTimeOffset, Func, Guid, IReadOnlyList, JsonSerializerOptions, object (+3 more)

### Community 3 - "Frontend API Client"
Cohesion: 0.09
Nodes (51): ApiErrorBody, buildExternalAuthUrl(), buildQuery(), createApiError(), createBodyReferencePhoto(), createOutfit(), deleteBodyReferencePhoto(), deleteGarment() (+43 more)

### Community 4 - "Schedule Service & Commands"
Cohesion: 0.08
Nodes (15): IReadOnlyCollection, CreateGarmentCommand, UpdateGarmentCommand, Guid, int, IReadOnlyList, string, WardrobeService (+7 more)

### Community 5 - "Background Removal Providers"
Cohesion: 0.09
Nodes (22): AutoBackgroundRemovalProvider, BackgroundRemovalRequest, BackgroundRemovalResult, Func, HttpClient, HttpRequestMessage, Image, Rgba32 (+14 more)

### Community 6 - "S3 Object Storage"
Cohesion: 0.21
Nodes (5): byte, DateTimeOffset, string, TimeSpan, LocalObjectStorage

### Community 7 - "Frontend Package Dependencies"
Cohesion: 0.05
Nodes (38): dependencies, date-fns, lucide-react, react, react-dom, react-router-dom, @tanstack/react-query, devDependencies (+30 more)

### Community 8 - "In-Memory Outfit Store"
Cohesion: 0.09
Nodes (8): Dictionary, DateOnly, DateTimeOffset, Guid, IReadOnlyList, object, InMemoryOutfitStore, InMemoryOutfitStoreSnapshot

### Community 9 - "Try-On Background Worker"
Cohesion: 0.07
Nodes (25): BackgroundService, Channel, IDatabase, CancellationToken, ILogger, Task, TryOnBackgroundWorker, CancellationToken (+17 more)

### Community 10 - "Test HTTP Recording Harness"
Cohesion: 0.07
Nodes (24): HttpMessageHandler, HttpResponseMessage, HttpStatusCode, IShareTokenGenerator, ReadOnlySpan, SecureShareTokenGenerator, byte, CancellationToken (+16 more)

### Community 11 - "Try-On Cost Estimator"
Cohesion: 0.13
Nodes (14): IReadOnlyDictionary, Guid, HashSet, IEnumerable, TryOnCostEstimate, TryOnCostEstimator, TryOnEstimateInput, CancellationToken (+6 more)

### Community 12 - "Outfit & Try-On Repositories"
Cohesion: 0.18
Nodes (3): Guid, ITryOnJobRepository, TryOnJob

### Community 13 - "Auth Route Guard"
Cohesion: 0.08
Nodes (15): AuthProvider, AuthSession, AppShell(), frontendRoot, registerServiceWorker(), RequireAuth(), authSessionQueryKey, useAuthSession() (+7 more)

### Community 14 - "Frontend Response Types"
Cohesion: 0.12
Nodes (23): ArrayItem, BodyReferencePhoto, BodyZone, CreatedBodyReferencePhoto, CreatedGarment, CreatedOutfit, JsonResponse, LaundryStatus (+15 more)

### Community 15 - "Outfit Utilities & Naming"
Cohesion: 0.13
Nodes (14): BodyReferenceManager(), garmentNameFromFile(), OutfitList(), CATEGORY_SELECTION_KEYS, groupGarmentsByCategory(), selectedGarmentIds(), selectionLabel(), garments (+6 more)

### Community 16 - "User Account Repository"
Cohesion: 0.11
Nodes (5): DateTimeOffset, IUserAccountRepository, AuthEmailVerificationToken, AuthPasswordResetToken, AuthSession

### Community 17 - "Wardrobe Filter Controls"
Cohesion: 0.16
Nodes (16): GarmentFilters, GarmentMetadataInput, colorOptions, seasonOptions, WardrobeFilters(), WardrobeFiltersProps, WardrobeViewMode, defaultWardrobeFilters (+8 more)

### Community 18 - "Backend NuGet Dependencies"
Cohesion: 0.09
Nodes (20): AWSSDK.S3 (4.0.24.2), dbup-postgresql (7.0.1), Microsoft.AspNetCore.Authentication.Google (10.0.2), Microsoft.AspNetCore.Authentication.OpenIdConnect (10.0.2), Microsoft.AspNetCore.OpenApi (10.0.2), Microsoft.Extensions.ApiDescription.Server (10.0.2), Microsoft.Extensions.Configuration.Abstractions (10.0.2), Npgsql (10.0.1) (+12 more)

### Community 19 - "API Request Contracts"
Cohesion: 0.08
Nodes (23): AuthProviderResponse, AuthSessionResponse, AuthUserResponse, CreateBodyReferencePhotoRequest, CreateGarmentRequest, CreateOutfitRequest, EmailVerificationRequest, EstimateTryOnRequest (+15 more)

### Community 20 - "Try-On Output Storage"
Cohesion: 0.11
Nodes (14): CancellationToken, DateTimeOffset, Guid, Task, ITryOnOutputStorage, CancellationToken, DateTimeOffset, Guid (+6 more)

### Community 21 - "Local Photo Storage"
Cohesion: 0.18
Nodes (5): IStoredPhotoReader, StoredImageVariant, StoredPhotoFile, TimeSpan, LocalPhotoStorage

### Community 22 - "HTTP Try-On Providers"
Cohesion: 0.05
Nodes (42): JsonElement, ITryOnProvider, TryOnGeneration, TryOnGenerationSettings, TryOnOptions, TryOnProviderCapabilities, TryOnProviderRequest, UserGender (+34 more)

### Community 23 - "Garment Repository & Search"
Cohesion: 0.15
Nodes (3): IGarmentRepository, GarmentQuery, GarmentItem

### Community 24 - "Photo Upload Service"
Cohesion: 0.17
Nodes (8): long, IncomingPhoto, IPhotoStorage, StoredPhoto, HashSet, ReadOnlySpan, PhotoUploadService, CountingPhotoStorage

### Community 25 - "Outfit Service"
Cohesion: 0.09
Nodes (13): IClock, OutfitQuery, UpdateOutfitCommand, Guid, IEnumerable, int, IReadOnlyList, OutfitService (+5 more)

### Community 26 - "Image Processor"
Cohesion: 0.22
Nodes (6): Image, int, Rgba32, ImageProcessor, Rectangle, Stream

### Community 27 - "Upload Queue UI"
Cohesion: 0.16
Nodes (10): UploadQueue(), UploadQueueProps, UploadQueueTextDraft, UploadQueueItem, UploadQueueItemUpdates, parseTokenText(), syncDefaultsTextDraft(), tokenListSignature() (+2 more)

### Community 28 - "App Shell & Account UI"
Cohesion: 0.14
Nodes (8): AuthUser, UserGender, AccountAvatar(), accountName(), AccountPanel(), ThemeMode, ThemeToggle(), ThemeToggleProps

### Community 29 - "Wardrobe Upload Logic"
Cohesion: 0.13
Nodes (25): TagChipsEditor(), TagChipsEditorProps, cleanPhotoChecklist, createRandomId(), createUploadQueueItem(), createUploadQueueItemId(), createUploadQueueItems(), getPhotoQualityWarnings() (+17 more)

### Community 30 - "Schedule & Share Repositories"
Cohesion: 0.14
Nodes (4): IShareLinkRepository, Guid, ShareService, ShareLink

### Community 31 - "TS App tsconfig"
Cohesion: 0.11
Nodes (17): compilerOptions, allowJs, allowSyntheticDefaultImports, esModuleInterop, forceConsistentCasingInFileNames, isolatedModules, jsx, lib (+9 more)

### Community 32 - "FASHN Try-On Provider"
Cohesion: 0.15
Nodes (3): NpgsqlDataReader, Action, Guid

### Community 33 - "Garment Card & Editor UI"
Cohesion: 0.36
Nodes (5): createGarment(), UpdateGarmentInput, UploadedPhotoResponse, garmentPhotoUrlsFromUpload(), wardrobeQueryKey

### Community 34 - "Shared Outfit Page"
Cohesion: 0.16
Nodes (10): getSharedOutfit(), Expect, frontendRoot, GetSharedOutfitReturnsSharedOutfit, operationSection(), repoRoot, responseSection(), SharePage() (+2 more)

### Community 35 - "Stored Photo URL Refresher"
Cohesion: 0.15
Nodes (4): IStoredPhotoUrlRefresher, string, TimeSpan, StoredPhotoUrlRefresher

### Community 36 - "Storage & Deploy Concepts"
Cohesion: 0.22
Nodes (9): Garment Background Removal Providers, Same-Origin /api and /uploads Convention, Signed Object URLs for Uploads, Production Docker Compose Stack, Self-Host Override (rembg + FASHN), Production Deploy Plan, Cloudflare + VPS + Docker Architecture, Local Signed Storage Public URL for FASHN (+1 more)

### Community 37 - "Photo Storage Abstractions"
Cohesion: 0.32
Nodes (3): GarmentRotationRender, IImageProcessor, ProcessedPhotoSet

### Community 38 - "Community 38"
Cohesion: 0.15
Nodes (7): EagerGarmentUploads, QueueUpdater, useEagerGarmentUploads(), useWardrobeMutations(), defaultUploadDefaults, garmentsResponse, WardrobePage()

### Community 39 - "Calendar UI & Utils"
Cohesion: 0.23
Nodes (7): buildMonthCalendar(), CalendarGridDay, weekDayLabels, OutfitChoiceList(), CalendarPage(), dateFromIso(), EditorialDatePicker()

### Community 41 - "OpenAPI Client Generator"
Cohesion: 0.18
Nodes (13): apiProject, frontendRoot, generateOpenApiDocument(), newestJsonFile(), openApiCache, openApiDir, openApiTypescriptBin, openApiTypescriptBinForShell (+5 more)

### Community 42 - "Wardrobe Dark Theme (Final)"
Cohesion: 0.21
Nodes (13): Add Garment Form Panel, Dark Theme Editorial UI, Garment Count Stats (Tops Bottoms Pieces), Left Navigation Sidebar, Nav Links Wardrobe Builder Calendar, Add Piece Primary Button, Purple/Violet Accent Palette, Outfit Planner Wardrobe Dark Theme (Final Contrast) (+5 more)

### Community 43 - "Wardrobe Dark Theme (After)"
Cohesion: 0.21
Nodes (12): Add Garment Panel, Improved Contrast State, Dark Theme Palette, Editorial Serif Hero Heading, Primary Action Button (Add piece), Wardrobe Dark Theme Contrast (After), Navigation Sidebar, Catalog Stats (Tops/Bottoms/Pieces) (+4 more)

### Community 44 - "Try-On Cost & Admin Concepts"
Cohesion: 0.15
Nodes (13): Account Profile (username/avatar/gender), Backend Onion Projects (Domain/Application/Infrastructure/Api), Try-On Job Caching, Try-On Item Classification (body vs visual-only), Try-On Modes, Try-On Cost Estimator Implementation Plan, Backend-Enforced Cost Confirmation, Try-On Cost Estimator Design Spec (+5 more)

### Community 45 - "Wardrobe Sidebar Dark Theme"
Cohesion: 0.20
Nodes (11): Add Garment Panel, Dark Theme UI, Editorial Fashion UI System, Garment Type Selector (Top/Bottom), Wardrobe Builder Calendar Nav Items, Purple/Pink Accent Palette, Dark Theme Sidebar Contrast Screenshot, App Shell Sidebar Navigation (+3 more)

### Community 46 - "Garment Category Controls"
Cohesion: 0.22
Nodes (7): GarmentCategory, SlotPicker(), GARMENT_CATEGORIES, SuggestedTagInput, UploadQueueDefaults, WardrobeUploadDefaults, GarmentCategoryIcon()

### Community 47 - "Wardrobe Button Contrast UI"
Cohesion: 0.27
Nodes (10): Button Contrast/Legibility Review Focus, Garment Count Stat Circles (Tops/Bottoms/Pieces), Editorial Dark Theme Palette, Instrument Serif Editorial Display Heading, Empty Category State (No tops yet), Top Navigation Icon Bar (Wardrobe/Upload/TryOn/Calendar/Theme), Pink Primary Action Color Convention, Active Pink/Purple Primary Icon Button (Wardrobe) (+2 more)

### Community 48 - "API Launch Settings"
Cohesion: 0.20
Nodes (9): ASPNETCORE_ENVIRONMENT, applicationUrl, commandName, dotnetRunMessages, environmentVariables, launchBrowser, profiles, OutfitPlanner.Api (+1 more)

### Community 50 - "TS Node tsconfig"
Cohesion: 0.20
Nodes (9): compilerOptions, allowSyntheticDefaultImports, composite, module, moduleResolution, skipLibCheck, strict, types (+1 more)

### Community 51 - "Google OAuth Handler"
Cohesion: 0.25
Nodes (6): GoogleHandler, OAuthCodeExchangeContext, OAuthTokenResponse, CanonicalGoogleHandler, IConfiguration, Task

### Community 52 - "Community 52"
Cohesion: 0.17
Nodes (9): AmazonS3Client, bool, IAmazonS3, IDisposable, string, TimeSpan, MinioObjectStorage, MinioObjectStorageSettings (+1 more)

### Community 53 - "Community 53"
Cohesion: 0.15
Nodes (3): NpgsqlConnection, NpgsqlTransaction, T

### Community 54 - "Auth Foundation Concepts"
Cohesion: 0.08
Nodes (28): Cookie-Based Auth (outfit_session + outfit_csrf), Dev Docker Compose Stack, App Shell / Route Page Split, Builder Stale Active Outfit Fix, Foundation First Implementation Plan, OpenAPI Client Generation Workflow, PWA Foundation (manifest/SW/offline), RequireAuth Route Guard (+20 more)

### Community 56 - "Outfit Composition Rules"
Cohesion: 0.18
Nodes (3): IOutfitRepository, Outfit, IEnumerable

### Community 57 - "Community 57"
Cohesion: 0.18
Nodes (3): IReadOnlyList, IBodyReferencePhotoRepository, BodyReferencePhoto

### Community 59 - "Community 59"
Cohesion: 0.23
Nodes (8): formFromGarment(), GarmentEditor(), GarmentEditorFormState, GarmentEditorSaveInput, sourceFromGarment(), normalizeDegrees(), RotateControl(), RotateControlProps

### Community 60 - "Editorial Visual System"
Cohesion: 0.40
Nodes (5): Editorial Fashion Visual System (Crimson Plinth), Claymorphism Removal Rule, Wardrobe UX Implementation Plan, Command Center with Upload Rail, Wardrobe UX Design Spec

### Community 61 - "Postgres Connection Probe"
Cohesion: 0.33
Nodes (4): CancellationToken, IConfiguration, Task, PostgresConnectionProbe

### Community 62 - "Community 62"
Cohesion: 0.25
Nodes (6): double, byte, Image, int, Rgba32, GarmentDeskew

### Community 63 - "Image File Validation"
Cohesion: 0.67
Nodes (4): isSupportedImageFile(), readImageFileAsDataUrl(), supportedImageTypes, validateUploadImageFile()

### Community 64 - "rembg Server Script"
Cohesion: 0.47
Nodes (4): multipart_prewarm_body(), prewarm_png(), prewarm_server(), Start rembg's HTTP server with ONNX Runtime diagnostics.  This wrapper is useful

### Community 67 - "Postgres Migration Runner"
Cohesion: 0.40
Nodes (3): ILogger, string, PostgresMigrationRunner

### Community 68 - "Wardrobe Page Tests"
Cohesion: 0.15
Nodes (11): Agents Context, Common Commands, Durable Rules, graphify, Project Context, Claude Code Context, Common Commands, Durable Rules (+3 more)

### Community 70 - "Design Token Tests"
Cohesion: 0.50
Nodes (3): shellCss, stylesCss, wardrobeCss

### Community 71 - "App Icon Branding"
Cohesion: 1.00
Nodes (3): Outfit Planner App Icon (SVG), Garment / Outfit Motif, Outfit Planner App Branding

### Community 85 - "Community 85"
Cohesion: 0.16
Nodes (5): DateOnly, IOutfitScheduleRepository, IReadOnlyList, OutfitItem, ScheduledOutfit

### Community 86 - "Community 86"
Cohesion: 0.33
Nodes (3): GarmentRotationOutcome, IGarmentImageRotator, RecordingGarmentImageRotator

### Community 89 - "Community 89"
Cohesion: 0.28
Nodes (5): GarmentItem, GarmentCard(), GarmentCardProps, GarmentEditorProps, EmptyState()

### Community 90 - "Community 90"
Cohesion: 0.14
Nodes (7): TimeSpan, IObjectStorage, IStoredPhotoDeletion, ObjectStoragePutRequest, ProcessedImage, StoredObject, StoredObjectFile

## Knowledge Gaps
- **235 isolated node(s):** `CreateBodyReferencePhotoRequest`, `CreateGarmentRequest`, `UpdateGarmentRequest`, `CreateOutfitRequest`, `UpdateOutfitRequest` (+230 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **25 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `TryOnService` connect `Try-On Cost Estimator` to `Stored Photo URL Refresher`, `Try-On Background Worker`, `Outfit & Try-On Repositories`, `User Account Repository`, `Try-On Output Storage`, `HTTP Try-On Providers`, `Outfit Composition Rules`, `Outfit Service`, `Community 57`?**
  _High betweenness centrality (0.117) - this node is a cross-community bridge._
- **Why does `PostgresOutfitStore` connect `PostgreSQL Outfit Store` to `FASHN Try-On Provider`, `User Account Persistence`, `Outfit & Try-On Repositories`, `User Account Repository`, `External Login Persistence`, `Community 85`, `Community 53`, `Garment Repository & Search`, `Outfit Composition Rules`, `Community 57`, `Community 87`, `Schedule & Share Repositories`?**
  _High betweenness centrality (0.062) - this node is a cross-community bridge._
- **Why does `IUserAccountRepository` connect `User Account Repository` to `PostgreSQL Outfit Store`, `Auth Service & Security`, `File-Backed Outfit Store`, `User Account Persistence`, `In-Memory Outfit Store`, `Try-On Cost Estimator`, `External Login Persistence`, `Community 85`?**
  _High betweenness centrality (0.048) - this node is a cross-community bridge._
- **What connects `CreateBodyReferencePhotoRequest`, `CreateGarmentRequest`, `UpdateGarmentRequest` to the rest of the system?**
  _243 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `PostgreSQL Outfit Store` be split into smaller, more focused modules?**
  _Cohesion score 0.1349206349206349 - nodes in this community are weakly interconnected._
- **Should `Auth Service & Security` be split into smaller, more focused modules?**
  _Cohesion score 0.05817028027498678 - nodes in this community are weakly interconnected._
- **Should `File-Backed Outfit Store` be split into smaller, more focused modules?**
  _Cohesion score 0.08325624421831637 - nodes in this community are weakly interconnected._