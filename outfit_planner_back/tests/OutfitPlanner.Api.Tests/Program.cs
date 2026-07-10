using OutfitPlanner.Application.Abstractions;
using OutfitPlanner.Application.Services;
using OutfitPlanner.Domain;
using OutfitPlanner.Infrastructure.AutoTagging;
using OutfitPlanner.Infrastructure.Security;
using OutfitPlanner.Infrastructure.Storage;
using OutfitPlanner.Infrastructure.TryOn;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

var tests = new List<(string Name, Action Test)>
{
    ("domain layer has no application infrastructure or api references", TestDomainLayerHasNoOuterReferences),
    ("application layer has no infrastructure or api references", TestApplicationLayerHasNoOuterReferences),
    ("dockerfile copies database schema into build context", TestDockerfileCopiesDatabaseSchema),
    ("docker compose uses postgres 18 compatible volume path", TestDockerComposeUsesPostgres18CompatibleVolumePath),
    ("docker compose disables postgres gss encryption", TestDockerComposeDisablesPostgresGssEncryption),
    ("docker compose keeps infrastructure ports internal in production", TestDockerComposeKeepsInfrastructurePortsInternalInProduction),
    ("development docker publishes postgres on a non-default host port", TestDevelopmentDockerPublishesPostgresOnNonDefaultHostPort),
    ("frontend docker config proxies api through same origin", TestFrontendDockerConfigProxiesApiThroughSameOrigin),
    ("production docker terminates https at frontend proxy", TestProductionDockerTerminatesHttpsAtFrontendProxy),
    ("development docker publishes https frontend and api defaults", TestDevelopmentDockerPublishesHttpsDefaults),
    ("dotnet run defaults api to https localhost 5001", TestDotnetRunDefaultsToHttpsLocalhost5001),
    ("vite dev defaults to https proxy target", TestViteDevDefaultsToHttpsProxyTarget),
    ("postgres store implements application repository ports", TestPostgresStoreImplementsRepositoryPorts),
    ("local file store implements application repository ports", TestLocalFileStoreImplementsRepositoryPorts),
    ("local file store persists records across restarts", TestLocalFileStorePersistsRecordsAcrossRestarts),
    ("local file store drops retired hat records from old snapshots", TestLocalFileStoreDropsRetiredHatRecords),
    ("api uses local file store when postgres is not configured", TestApiUsesLocalFileStoreWithoutPostgres),
    ("api fails closed without a local object storage signing secret", TestApiFailsClosedWithoutObjectStorageSigningSecret),
    ("api hardens abuse and security response surfaces", TestApiHardensAbuseAndSecuritySurfaces),
    ("api maps validation exceptions to bad request", TestApiMapsValidationExceptionsToBadRequest),
    ("validation failures raise a validation exception", TestValidationFailuresRaiseValidationException),
    ("postgres schema contains tables required by repository ports", TestPostgresSchemaContainsRepositoryTables),
    ("postgres schema contains production auth tables and indexes", TestPostgresSchemaContainsAuthTables),
    ("postgres schema contains user account profile fields", TestPostgresSchemaContainsUserAccountProfileFields),
    ("auth service registers email users with hashed passwords and sessions", TestAuthServiceRegistersEmailUsers),
    ("auth service updates username avatar and gender profile fields", TestAuthServiceUpdatesAccountProfile),
    ("auth service requires password length digit and letter only", TestAuthServicePasswordPolicy),
    ("auth service rejects duplicate email registration", TestAuthServiceRejectsDuplicateEmailRegistration),
    ("auth service signs in existing external accounts and auto-registers missing accounts", TestAuthServiceExternalLoginAutoRegisters),
    ("auth service revokes session tokens by stored hash", TestAuthServiceRevokesSessionByHash),
    ("auth service lists sessions revokes all sessions and cleans expired sessions", TestAuthServiceSessionHardening),
    ("user roles default to free and pinned emails override stored roles", TestUserRolesDefaultToFreeAndPinnedEmailsOverride),
    ("admin service lists searches filters and counts users", TestAdminServiceListsSearchesAndCountsUsers),
    ("admin service changes roles with pinned and self guards", TestAdminServiceChangesRolesWithGuards),
    ("admin service delete guards protect pinned and self accounts", TestAdminServiceDeleteGuards),
    ("postgres schema contains account roles", TestPostgresSchemaContainsAccountRoles),
    ("api exposes role-gated admin endpoints", TestApiExposesAdminEndpoints),
    ("credit ledger grants debits and refunds with admin bypass", TestCreditLedgerGrantsDebitsAndRefunds),
    ("trial grants top up existing accounts to the configured amount", TestTrialGrantTopsUpToConfiguredAmount),
    ("billing rules map subscription statuses to premium", TestBillingRulesMapStatuses),
    ("billing service gates checkout portal and top-ups by plan", TestBillingServiceGatesCheckoutAndPortal),
    ("billing webhooks upsert subscriptions transition roles and grant top-ups idempotently", TestBillingWebhooksDriveRolesAndTopUps),
    ("local file store persists billing records across restarts", TestLocalFileStorePersistsBillingRecords),
    ("postgres schema contains billing tables", TestPostgresSchemaContainsBillingTables),
    ("entitlement caps block creation at plan limits", TestEntitlementCapsBlockCreation),
    ("try-on estimate applies plan modes and resolution pricing", TestTryOnEstimateAppliesPlanModesAndResolution),
    ("try-on start debits credits and failures refund", TestStartTryOnDebitsCreditsCacheHitsAndRefunds),
    ("try-on queue prioritizes premium jobs", TestTryOnQueuePrioritizesPremiumJobs),
    ("postgres schema contains account credit ledger", TestPostgresSchemaContainsCreditLedger),
    ("api exposes paywall endpoints", TestApiExposesPaywallEndpoints),
    ("api exposes secure auth endpoints and cookie settings", TestApiExposesSecureAuthEndpoints),
    ("api exposes privacy and auth hardening endpoints", TestApiExposesPrivacyAndAuthHardeningEndpoints),
    ("api exposes edit delete filtering and revoke endpoints", TestApiExposesEditDeleteFilterAndRevokeEndpoints),
    ("api exposes gender-filtered hairstyle preset endpoints", TestApiExposesHairstylePresetEndpoints),
    ("hairstyle preset catalog serves the vendored manifest", TestHairstylePresetCatalogServesVendoredManifest),
    ("api exposes openapi document generation", TestApiExposesOpenApiDocumentGeneration),
    ("api documents frontend response bodies for generated types", TestApiDocumentsFrontendResponseBodies),
    ("maps expanded garment categories to richer body zones", TestCategoryMapping),
    ("wardrobe service updates structured garment metadata without reupload", TestWardrobeServiceUpdatesStructuredMetadata),
    ("wardrobe service auto-straightens clothing categories on create only", TestWardrobeServiceAutoStraightensClothingOnly),
    ("wardrobe service rotates and persists garment rotation on update", TestWardrobeServiceRotatesGarmentOnUpdate),
    ("wardrobe service persists and refreshes garment cutout measurement", TestWardrobeServicePersistsAndRefreshesCutoutMeasurement),
    ("wardrobe service filters sorts and paginates garments", TestWardrobeServiceFiltersSortsAndPaginatesGarments),
    ("outfit service updates gets filters and deletes outfits", TestOutfitServiceUpdatesFiltersAndDeletesOutfits),
    ("outfit service persists composed figure state", TestOutfitServicePersistsComposedFigureState),
    ("outfit service keeps person preview on metadata update", TestOutfitServiceKeepsPersonPreviewOnMetadataUpdate),
    ("outfit items carry garment cutout measurements", TestOutfitItemsCarryGarmentCutoutMeasurements),
    ("postgres schema declares outfit composed figure columns", TestPostgresSchemaContainsComposedFigureColumns),
    ("outfit service applies slot compatibility rules", TestOutfitSlotCompatibilityRules),
    ("outfit rules carry garment rotation onto outfit items", TestOutfitRulesPreservesGarmentRotation),
    ("schedule service can unschedule a planned date", TestScheduleServiceUnschedulesDate),
    ("share service can revoke current user share links", TestShareServiceRevokesShareLinks),
    ("try-on estimator classifies outfit items and prices modes", TestTryOnCostEstimatorClassifiesAndPricesModes),
    ("try-on cache key varies with garment rotation", TestTryOnCacheKeyVariesWithGarmentRotation),
    ("try-on estimator marks unavailable modes", TestTryOnCostEstimatorMarksUnavailableModes),
    ("try-on service requires explicit AI consent before provider call", TestTryOnConsentRequired),
    ("try-on service blocks ai generation until user gender is set", TestTryOnServiceBlocksAiUntilGenderIsSet),
    ("try-on service estimates cost before generation", TestTryOnServiceEstimatesCost),
    ("try-on service marks provider unsupported modes unavailable", TestTryOnServiceMarksProviderUnsupportedModesUnavailable),
    ("try-on service exposes only confirmed start contract", TestTryOnServiceExposesOnlyConfirmedStartContract),
    ("try-on service enforces confirmed credits and cache key", TestTryOnServiceEnforcesConfirmedCost),
    ("try-on service returns cache hits without queueing provider work", TestTryOnServiceReturnsCacheHitsWithoutQueueing),
    ("try-on service deletes active preview output from outfit", TestTryOnServiceDeletesActivePreviewOutputFromOutfit),
    ("try-on service deletes active preview output by outfit", TestTryOnServiceDeletesActivePreviewOutputByOutfit),
    ("try-on service sends cutout garment image to provider", TestTryOnServiceSendsCutoutGarmentImageToProvider),
    ("try-on service completes clothes-only preview without ai", TestTryOnServiceCompletesClothesOnlyWithoutAi),
    ("try-on service completes clothes-only preview without body reference", TestTryOnServiceCompletesClothesOnlyWithoutBodyReference),
    ("try-on service queues jobs without calling provider inline", TestTryOnServiceQueuesJobsWithoutInlineProviderCall),
    ("try-on processor completes queued jobs through provider", TestTryOnProcessorCompletesQueuedJobs),
    ("try-on processor sends public absolute storage urls to external providers", TestTryOnProcessorSendsPublicStorageUrlsToProvider),
    ("try-on processor stores external provider outputs before exposing them", TestTryOnProcessorStoresExternalProviderOutputs),
    ("try-on processor excludes visual-only items outside composite mode", TestTryOnProcessorExcludesVisualOnlyItemsOutsideCompositeMode),
    ("try-on processor passes user gender to provider requests", TestTryOnProcessorPassesGenderToProvider),
    ("try-on service forwards sequential flow option to provider", TestTryOnServiceForwardsSequentialFlowOption),
    ("api registers redis try-on queue and provider choices", TestApiRegistersRedisQueueAndProviderChoices),
    ("api maps dot env FASHN aliases into canonical config", TestApiMapsDotEnvFashnAliases),
    ("schedule service stores one planned outfit per user and day", TestDailySchedulePerUser),
    ("share token generator emits url safe high entropy tokens", TestShareTokenGenerator),
    ("photo upload service rejects unsupported content types", TestPhotoUploadRejectsUnsupportedContentType),
    ("photo upload service rejects forged image content type by magic bytes", TestPhotoUploadRejectsForgedImageContentType),
    ("photo upload service accepts large phone photos", TestPhotoUploadAcceptsLargePhonePhotos),
    ("api configures upload body limits", TestApiConfiguresUploadBodyLimits),
    ("api exposes test diagnostics and trace ids", TestApiExposesTestDiagnosticsAndTraceIds),
    ("object storage ports and local/minio adapters exist", TestObjectStoragePortsAndAdapters),
    ("local object storage can emit public absolute signed urls", TestLocalObjectStorageEmitsPublicAbsoluteSignedUrls),
    ("try-on output storage port and adapter exist", TestTryOnOutputStoragePortAndAdapter),
    ("image processing pipeline exposes privacy preserving variants", TestImageProcessingPipelineContracts),
    ("garment processing emits an immutable base cutout variant", TestImageProcessorEmitsBaseCutoutVariant),
    ("garment processing trims the cutout to its alpha bounding box and measures it", TestGarmentProcessingMeasuresAndTrimsCutout),
    ("cutout measurement is invariant to shooting distance and padding", TestMeasureGarmentCutoutIsScaleInvariant),
    ("cutout trim ignores scattered background specks", TestCutoutTrimIgnoresScatteredBackgroundSpecks),
    ("garment cutout crops past scattered background grain", TestGarmentCutoutCropsPastScatteredBackgroundGrain),
    ("simple background removal preserves alpha on opaque source", TestSimpleBackgroundRemovalPreservesAlphaOnOpaqueSource),
    ("garment deskew straightens a tilted silhouette", TestGarmentDeskewStraightensTiltedSilhouette),
    ("garment deskew skips square and extreme tilts", TestGarmentDeskewSkipsSquareAndExtremeTilt),
    ("garment rotation render produces rotated variants", TestImageProcessorRendersRotatedGarmentVariants),
    ("background removal provider contracts exist", TestBackgroundRemovalProviderContracts),
    ("background removal auto provider prefers rembg when available", TestBackgroundRemovalAutoProviderPrefersRembg),
    ("api defaults background removal provider to auto", TestApiDefaultsBackgroundRemovalToAuto),
    ("image processor delegates garment cutouts to background removal provider", TestImageProcessorDelegatesGarmentCutout),
    ("http background removal provider posts multipart image with api key", TestHttpBackgroundRemovalProviderPostsMultipartImageWithApiKey),
    ("rembg server provider posts multipart file field", TestRembgServerProviderPostsMultipartFileField),
    ("api registers rembg server provider", TestApiRegistersRembgServerProvider),
    ("single garment extraction scaffold returns one cutout", TestSingleGarmentExtractionScaffoldReturnsOneCutout),
    ("garment auto-tagger provider contracts exist", TestGarmentAutoTaggerContractsExist),
    ("http garment auto-tagger parses classification response", TestHttpGarmentAutoTaggerParsesClassification),
    ("http garment auto-tagger drops categories outside the enum", TestHttpGarmentAutoTaggerMapsUnknownCategoryToNull),
    ("http garment auto-tagger surfaces provider errors", TestHttpGarmentAutoTaggerThrowsOnErrorStatus),
    ("disabled garment auto-tagger returns empty suggestions", TestDisabledGarmentAutoTaggerReturnsEmpty),
    ("auto garment auto-tagger routes by service health", TestAutoGarmentAutoTaggerRoutesByHealth),
    ("garment auto-tag service resolves a clean cutout and never throws", TestGarmentAutoTagServiceResolvesCleanCutout),
    ("api wires auto-tagging classify endpoint and defaults", TestApiWiresAutoTaggingClassifyEndpoint),
    ("photo upload service stores garment photo variants behind signed url", TestPhotoUploadStoresGarmentPhoto),
    ("stored photo urls refresh stale garment links to cutouts", TestStoredPhotoUrlRefresherRefreshesGarmentVariants),
    ("photo upload service stores body reference photo privately", TestPhotoUploadStoresBodyReferencePhoto),
    ("wardrobe service deletes garment records and stored photos", TestWardrobeServiceDeletesGarmentAndStoredPhoto),
    ("wardrobe service deletes body reference records and stored photos", TestWardrobeServiceDeletesBodyReferenceAndStoredPhoto),
    ("garment rotation works against non-file object storage", TestGarmentRotationWorksOnNonFileObjectStorage),
    ("wardrobe service purges all stored photos for a user", TestWardrobeServicePurgesAllUserStoredPhotos),
    ("postgres schema contains structured garment metadata and query indexes", TestPostgresSchemaContainsStructuredMetadataAndIndexes),
    ("postgres schema declares garment cutout measurement columns", TestPostgresSchemaContainsCutoutMeasurementColumns),
    ("hat category is fully retired from schema and migrations", TestHatCategoryFullyRetired),
    ("postgres schema contains cascade and cleanup indexes", TestPostgresSchemaContainsCascadeAndCleanupIndexes),
    ("postgres schema contains privacy storage auth hardening and try-on retention fields", TestPostgresSchemaContainsPrivacyStorageAuthAndRetentionFields),
    ("try-on storage persists mode cost and cache metadata", TestTryOnStoragePersistsModeCostAndCacheMetadata),
    ("api uses DbUp migrations instead of startup schema initializer", TestApiUsesDbUpMigrations),
    ("new try-on provider adapters implement provider port", TestProviderAdaptersImplementPort),
    ("json try-on provider posts mode-aware payloads without dropping base path", TestJsonProviderPayloadAndEndpointPath),
    ("json try-on provider capabilities are mode specific", TestJsonProviderCapabilitiesAreModeSpecific),
    ("json try-on provider rejects unsupported modes before network call", TestJsonProviderRejectsUnsupportedModesBeforeNetworkCall),
    ("fashn provider sends only body try-on items for normal modes", TestFashnProviderSendsOnlyBodyTryOnItems),
    ("fashn provider requires api key before network call", TestFashnProviderRequiresApiKey),
    ("fashn provider sends configured generation options", TestFashnProviderSendsConfiguredGenerationOptions),
    ("fashn default resolution charges base credits", TestFashnDefaultResolutionChargesBaseCredits),
    ("fashn provider sends tryon max quality gender prompt", TestFashnProviderSendsTryOnMaxQualityGenderPrompt),
    ("fashn provider omits prompt when no template configured", TestFashnProviderOmitsPromptWhenNoTemplateConfigured),
    ("fashn provider submits try-on request and polls status", TestFashnProviderSubmitsRequestAndPollsStatus),
    ("fashn provider rejects multi-garment outfits when sequential flow is off", TestFashnProviderRejectsMultiGarmentOutfitsWhenSequentialOff),
    ("fashn provider runs multi-garment outfits sequentially when enabled", TestFashnProviderRunsSequentialMultiGarmentOutfits)
};

var failures = 0;

foreach (var (name, test) in tests)
{
    try
    {
        test();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        failures++;
        Console.WriteLine($"FAIL {name}: {ex.Message}");
    }
}

if (failures > 0)
{
    Environment.ExitCode = 1;
}

static void TestCategoryMapping()
{
    AssertEqual(BodyZone.Torso, GarmentRules.GetBodyZone(GarmentCategory.Top), "Top should map to torso");
    AssertEqual(BodyZone.Legs, GarmentRules.GetBodyZone(GarmentCategory.Bottom), "Bottom should map to legs");
    AssertEqual(BodyZone.FullBody, GarmentRules.GetBodyZone(GarmentCategory.Dress), "Dress should map to full body");
    AssertEqual(BodyZone.OuterLayer, GarmentRules.GetBodyZone(GarmentCategory.Outerwear), "Outerwear should map to outer layer");
    AssertEqual(BodyZone.Feet, GarmentRules.GetBodyZone(GarmentCategory.Shoes), "Shoes should map to feet");
    AssertEqual(BodyZone.Accessory, GarmentRules.GetBodyZone(GarmentCategory.Bag), "Bag should map to accessory");
    AssertEqual(BodyZone.Accessory, GarmentRules.GetBodyZone(GarmentCategory.Accessory), "Accessory should map to accessory");
    AssertTrue(!Enum.IsDefined(typeof(GarmentCategory), "Hat"), "the Hat category is retired; head wear is covered by hairstyle presets");
}

static void TestDomainLayerHasNoOuterReferences()
{
    AssertAssemblyDoesNotReference(
        typeof(GarmentItem).Assembly.GetReferencedAssemblies().Select(name => name.Name ?? ""),
        new[] { "OutfitPlanner.Application", "OutfitPlanner.Infrastructure", "OutfitPlanner.Api" },
        "Domain must not depend on outer layers.");
}

static void TestApplicationLayerHasNoOuterReferences()
{
    AssertAssemblyDoesNotReference(
        typeof(OutfitService).Assembly.GetReferencedAssemblies().Select(name => name.Name ?? ""),
        new[] { "OutfitPlanner.Infrastructure", "OutfitPlanner.Api" },
        "Application must depend only inward and on abstractions.");
}

static void TestDockerfileCopiesDatabaseSchema()
{
    var dockerfilePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Dockerfile"));
    var dockerfile = File.ReadAllText(dockerfilePath);

    AssertTrue(dockerfile.Contains("COPY database/ database/", StringComparison.Ordinal), "Dockerfile should copy database/schema.sql before dotnet publish.");
    AssertTrue(dockerfile.Contains("COPY assets/ assets/", StringComparison.Ordinal), "Dockerfile should copy hairstyle assets before dotnet publish.");
}

static void TestDockerComposeUsesPostgres18CompatibleVolumePath()
{
    var composePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "docker-compose.yml"));
    var compose = File.ReadAllText(composePath);

    AssertTrue(compose.Contains("postgres_data:/var/lib/postgresql", StringComparison.Ordinal), "postgres 18 volume should mount at /var/lib/postgresql.");
    AssertTrue(!compose.Contains("postgres_data:/var/lib/postgresql/data", StringComparison.Ordinal), "postgres 18 volume should not mount directly at /var/lib/postgresql/data.");
    AssertTrue(compose.Contains("pg_isready", StringComparison.Ordinal), "api should wait for postgres healthcheck.");
}

static void TestDockerComposeDisablesPostgresGssEncryption()
{
    var composePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "docker-compose.yml"));
    var compose = File.ReadAllText(composePath);

    AssertTrue(compose.Contains("GSS Encryption Mode=Disable", StringComparison.Ordinal), "postgres connection string should disable GSS encryption in the Linux API container.");
}

static void TestDockerComposeKeepsInfrastructurePortsInternalInProduction()
{
    var composePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "docker-compose.yml"));
    var compose = File.ReadAllText(composePath);

    AssertTrue(!compose.Contains("\"5433:5432\"", StringComparison.Ordinal), "production postgres should not publish a host port.");
    AssertTrue(!compose.Contains("\"6379:6379\"", StringComparison.Ordinal), "production redis should not publish a host port.");
    AssertTrue(!compose.Contains("\"9000:9000\"", StringComparison.Ordinal), "production minio api should not publish a host port.");
    AssertTrue(!compose.Contains("\"9001:9001\"", StringComparison.Ordinal), "production minio console should not publish a host port.");
}

static void TestDevelopmentDockerPublishesPostgresOnNonDefaultHostPort()
{
    var composePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "docker-compose.dev.yml"));
    var compose = File.ReadAllText(composePath);

    AssertTrue(compose.Contains("\"5433:5432\"", StringComparison.Ordinal), "development postgres should publish on host port 5433 to avoid colliding with local PostgreSQL on Windows.");
}

static void TestFrontendDockerConfigProxiesApiThroughSameOrigin()
{
    var rootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".."));
    var compose = File.ReadAllText(Path.Combine(rootPath, "docker-compose.yml")).ReplaceLineEndings("\n");
    var dockerfile = File.ReadAllText(Path.Combine(rootPath, "outfit_planner_front", "Dockerfile"));
    var dockerignorePath = Path.Combine(rootPath, ".dockerignore");
    var nginx = File.ReadAllText(Path.Combine(rootPath, "outfit_planner_front", "nginx.conf"));

    AssertTrue(File.Exists(dockerignorePath), "root .dockerignore should exist because production frontend uses the repo root build context.");
    var dockerignore = File.ReadAllText(dockerignorePath);
    foreach (var pattern in new[]
    {
        "**/node_modules",
        "**/dist",
        "**/.generated",
        "**/bin",
        "**/obj",
        "**/.aspnet",
        "**/.secrets",
        "**/storage",
        "**/uploads",
        "**/*.log",
        ".git",
        ".superpowers"
    })
    {
        AssertTrue(dockerignore.Contains(pattern, StringComparison.Ordinal), $"root .dockerignore should exclude {pattern}.");
    }

    AssertTrue(compose.Contains("VITE_API_URL: /api", StringComparison.Ordinal), "frontend docker build should use same-origin /api.");
    AssertTrue(!compose.Contains("VITE_API_URL: http://localhost:5000/api", StringComparison.Ordinal), "frontend docker build should not bake cross-origin localhost API URL.");
    AssertTrue(compose.Contains("frontend:\n    build:\n      context: .\n      dockerfile: outfit_planner_front/Dockerfile", StringComparison.Ordinal), "production frontend should build from repo root with the frontend Dockerfile.");
    AssertTrue(dockerfile.Contains("FROM mcr.microsoft.com/dotnet/sdk:10.0 AS openapi", StringComparison.Ordinal), "frontend Dockerfile should generate OpenAPI in a dotnet SDK stage.");
    AssertTrue(dockerfile.Contains("COPY outfit_planner_back/ ./outfit_planner_back/", StringComparison.Ordinal), "frontend Dockerfile should copy backend sources before OpenAPI generation.");
    AssertTrue(dockerfile.Contains("OUTFIT_PLANNER_OPENAPI_DOCUMENT", StringComparison.Ordinal), "frontend Dockerfile should pass generated OpenAPI JSON to npm build.");
    AssertTrue(dockerfile.Contains("COPY outfit_planner_front/package*.json ./", StringComparison.Ordinal), "frontend Dockerfile should copy frontend package files from root context.");
    AssertTrue(dockerfile.Contains("COPY outfit_planner_front/ ./", StringComparison.Ordinal), "frontend Dockerfile should copy frontend sources from root context.");
    AssertTrue(nginx.Contains("location /api/", StringComparison.Ordinal), "frontend nginx should proxy /api requests.");
    AssertTrue(nginx.Contains("proxy_pass http://api:8080/api/", StringComparison.Ordinal), "frontend nginx should proxy to the api service.");
    AssertTrue(nginx.Contains("client_max_body_size 100m", StringComparison.Ordinal), "frontend nginx should allow large photo uploads through the proxy.");
}

static void TestProductionDockerTerminatesHttpsAtFrontendProxy()
{
    var rootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".."));
    var compose = File.ReadAllText(Path.Combine(rootPath, "docker-compose.yml"));
    var nginx = File.ReadAllText(Path.Combine(rootPath, "outfit_planner_front", "nginx.conf"));

    AssertTrue(compose.Contains("\"443:443\"", StringComparison.Ordinal), "production frontend should publish HTTPS on host port 443.");
    AssertTrue(compose.Contains("\"80:80\"", StringComparison.Ordinal), "production frontend should publish HTTP only for redirect to HTTPS.");
    AssertTrue(!compose.Contains("\"5000:8080\"", StringComparison.Ordinal), "production api should not publish a direct HTTP host port.");
    AssertTrue(!compose.Contains("\"5001:", StringComparison.Ordinal), "production api should stay behind the frontend TLS proxy instead of publishing a direct host port.");
    AssertTrue(compose.Contains("./.secrets/tls/fullchain.pem:/etc/nginx/certs/fullchain.pem:ro", StringComparison.Ordinal), "production nginx should mount the public TLS certificate.");
    AssertTrue(compose.Contains("./.secrets/tls/privkey.pem:/etc/nginx/certs/privkey.pem:ro", StringComparison.Ordinal), "production nginx should mount the private TLS key.");
    AssertTrue(nginx.Contains("listen 443 ssl", StringComparison.Ordinal), "production nginx should serve HTTPS.");
    AssertTrue(nginx.Contains("ssl_certificate /etc/nginx/certs/fullchain.pem", StringComparison.Ordinal), "production nginx should use the mounted certificate.");
    AssertTrue(nginx.Contains("return 301 https://$host$request_uri", StringComparison.Ordinal), "production nginx should redirect HTTP to HTTPS.");
    AssertTrue(nginx.Contains("proxy_set_header X-Forwarded-Proto $scheme", StringComparison.Ordinal), "frontend proxy should forward the browser-facing scheme to the API.");
}

static void TestDevelopmentDockerPublishesHttpsDefaults()
{
    var rootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".."));
    var compose = File.ReadAllText(Path.Combine(rootPath, "docker-compose.dev.yml"));

    AssertTrue(compose.Contains("ASPNETCORE_URLS: https://+:8081", StringComparison.Ordinal), "development api container should listen on HTTPS.");
    AssertTrue(compose.Contains("--no-launch-profile", StringComparison.Ordinal), "development api container should not let launchSettings bind localhost:5001 inside Docker.");
    AssertTrue(compose.Contains("\"5001:8081\"", StringComparison.Ordinal), "development api should publish HTTPS on host port 5001.");
    AssertTrue(!compose.Contains("\"5000:8080\"", StringComparison.Ordinal), "development api should not publish the old HTTP host port.");
    AssertTrue(compose.Contains("ASPNETCORE_Kestrel__Certificates__Default__Path: /https/outfit-planner-dev.pfx", StringComparison.Ordinal), "development api should load the shared dev HTTPS certificate.");
    AssertTrue(compose.Contains("VITE_DEV_HTTPS: \"true\"", StringComparison.Ordinal), "development frontend container should run Vite over HTTPS.");
    AssertTrue(compose.Contains("VITE_DEV_API_TARGET: https://api:8081", StringComparison.Ordinal), "development Vite proxy should target the HTTPS API service.");
    AssertTrue(compose.Contains("\"5173:5173\"", StringComparison.Ordinal), "development frontend should keep the HTTPS Vite host port.");
    AssertTrue(compose.Contains("./.aspnet/https:/https:ro", StringComparison.Ordinal), "development containers should share the exported local HTTPS certificate.");
}

static void TestDotnetRunDefaultsToHttpsLocalhost5001()
{
    var launchSettingsPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "OutfitPlanner.Api", "Properties", "launchSettings.json"));
    var launchSettings = File.ReadAllText(launchSettingsPath);

    AssertTrue(launchSettings.Contains("\"applicationUrl\": \"https://localhost:5001\"", StringComparison.Ordinal), "dotnet run should default the API to https://localhost:5001.");
}

static void TestViteDevDefaultsToHttpsProxyTarget()
{
    var rootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".."));
    var packageJson = File.ReadAllText(Path.Combine(rootPath, "outfit_planner_front", "package.json"));
    var viteConfig = File.ReadAllText(Path.Combine(rootPath, "outfit_planner_front", "vite.config.ts"));

    AssertTrue(packageJson.Contains("\"dev\": \"vite --host localhost --mode https\"", StringComparison.Ordinal), "npm run dev should start the HTTPS Vite server by default.");
    AssertTrue(viteConfig.Contains("process.env.VITE_DEV_API_TARGET ?? 'https://localhost:5001'", StringComparison.Ordinal), "Vite should proxy to the HTTPS API by default.");
    AssertTrue(viteConfig.Contains("VITE_DEV_HTTPS_PFX", StringComparison.Ordinal), "Vite should support the shared Docker HTTPS pfx certificate.");
}

static void TestPostgresStoreImplementsRepositoryPorts()
{
    var storeType = typeof(PostgresOutfitStore);
    var requiredPorts = new[]
    {
        typeof(IBodyReferencePhotoRepository),
        typeof(IGarmentRepository),
        typeof(IOutfitRepository),
        typeof(IOutfitScheduleRepository),
        typeof(ITryOnJobRepository),
        typeof(IShareLinkRepository),
        typeof(ICreditLedgerRepository),
        typeof(ISubscriptionRepository),
        typeof(IBillingEventRepository)
    };

    foreach (var port in requiredPorts)
    {
        AssertTrue(port.IsAssignableFrom(storeType), $"Postgres store should implement {port.Name}");
    }
}

static void TestLocalFileStoreImplementsRepositoryPorts()
{
    var storeType = typeof(FileBackedOutfitStore);
    var requiredPorts = new[]
    {
        typeof(IBodyReferencePhotoRepository),
        typeof(IGarmentRepository),
        typeof(IOutfitRepository),
        typeof(IOutfitScheduleRepository),
        typeof(ITryOnJobRepository),
        typeof(IShareLinkRepository),
        typeof(IUserAccountRepository)
    };

    foreach (var port in requiredPorts)
    {
        AssertTrue(port.IsAssignableFrom(storeType), $"Local file store should implement {port.Name}");
    }
}

static void TestLocalFileStorePersistsRecordsAcrossRestarts()
{
    var tempPath = Path.Combine(Path.GetTempPath(), "outfit-planner-local-store-tests", Guid.NewGuid().ToString("N"));

    try
    {
        var snapshotPath = Path.Combine(tempPath, "outfit-store.json");
        var now = DateTimeOffset.UtcNow;
        var day = new DateOnly(2026, 6, 24);
        var user = new UserAccount(
            "usr_local",
            "local@example.com",
            "local@example.com",
            "Local User",
            "hashed-password",
            now,
            now,
            null);
        var bodyPhoto = new BodyReferencePhoto(
            Guid.Parse("40000000-0000-0000-0000-000000000001"),
            user.Id,
            "/api/storage/signed/body-reference-photos/original/body.png?expires=1&signature=test",
            now);
        var garment = new GarmentItem(
            Guid.Parse("40000000-0000-0000-0000-000000000002"),
            user.Id,
            "linen shirt",
            GarmentCategory.Top,
            BodyZone.Torso,
            "/api/storage/signed/garments/processed-cutout/shirt.png?expires=1&signature=test",
            "/api/storage/signed/garments/thumbnail/shirt.png?expires=1&signature=test",
            new[] { "summer" },
            "white",
            Array.Empty<string>(),
            "linen",
            null,
            null,
            new[] { "summer" },
            null,
            null,
            Array.Empty<string>(),
            null,
            null,
            null,
            false,
            false,
            null,
            "clean",
            now);
        var outfit = new Outfit(
            Guid.Parse("40000000-0000-0000-0000-000000000003"),
            user.Id,
            "saved outfit",
            new[] { new OutfitItem(garment.Id, garment.Name, garment.Category, garment.BodyZone, garment.ThumbnailUrl) },
            Array.Empty<string>(),
            Array.Empty<string>(),
            false,
            false,
            null,
            "/api/storage/signed/try-on-output/preview.png?expires=1&signature=test",
            now);
        var job = new TryOnJob(
            Guid.Parse("40000000-0000-0000-0000-000000000004"),
            user.Id,
            outfit.Id,
            bodyPhoto.ImageUrl,
            SequentialFlowEnabled: true,
            TryOnStatus.Succeeded,
            "provider-job",
            outfit.PersonPreviewUrl,
            null,
            now,
            now)
        {
            ProviderName = "FashnTryOnProvider",
            SourceBodyPhotoId = bodyPhoto.Id,
            TryOnMode = TryOnMode.SequentialOutfitTryOn,
            ConfirmedCredits = 1,
            CacheKey = "cache-key",
            ProviderSettingsHash = "settings"
        };

        var first = new FileBackedOutfitStore(snapshotPath);
        first.AddUser(user);
        first.AddAuthSession(new AuthSession(
            Guid.Parse("40000000-0000-0000-0000-000000000005"),
            user.Id,
            "session-hash",
            "csrf-hash",
            now.AddDays(1),
            now,
            null));
        first.AddBodyReferencePhoto(bodyPhoto);
        first.AddGarment(garment);
        first.AddOutfit(outfit);
        first.UpsertScheduledOutfit(new ScheduledOutfit(
            Guid.Parse("40000000-0000-0000-0000-000000000006"),
            user.Id,
            day,
            outfit.Id,
            now));
        first.AddTryOnJob(job);

        AssertTrue(File.Exists(snapshotPath), "local file store should write a snapshot file after mutations.");

        var restarted = new FileBackedOutfitStore(snapshotPath);

        AssertEqual(user.Id, restarted.GetUserByNormalizedEmail("local@example.com")?.Id ?? "", "local file store should restore users.");
        AssertTrue(restarted.GetActiveAuthSessionByTokenHash("session-hash", now) is not null, "local file store should restore auth sessions.");
        AssertEqual(bodyPhoto.Id, restarted.ListBodyReferencePhotosByUser(user.Id)[0].Id, "local file store should restore body photos.");
        AssertEqual(garment.Id, restarted.ListGarmentsByUser(user.Id)[0].Id, "local file store should restore garments.");
        AssertEqual(outfit.Id, restarted.ListOutfitsByUser(user.Id)[0].Id, "local file store should restore outfits.");
        AssertEqual(outfit.Id, restarted.ListScheduleByUser(user.Id, day, day)[0].OutfitId, "local file store should restore schedule entries.");
        AssertEqual(job.Id, restarted.GetTryOnJobByUser(user.Id, job.Id)?.Id ?? Guid.Empty, "local file store should restore try-on jobs.");
    }
    finally
    {
        if (Directory.Exists(tempPath))
        {
            Directory.Delete(tempPath, recursive: true);
        }
    }
}

static void TestLocalFileStoreDropsRetiredHatRecords()
{
    var tempPath = Path.Combine(Path.GetTempPath(), "outfit-planner-local-store-tests", Guid.NewGuid().ToString("N"));

    try
    {
        var snapshotPath = Path.Combine(tempPath, "outfit-store.json");
        var store = new FileBackedOutfitStore(snapshotPath);
        var top = store.CreateGarment(CreateGarment("user-a", "linen shirt", GarmentCategory.Top));
        var legacyHat = store.CreateGarment(CreateGarment("user-a", "legacy hat", GarmentCategory.Accessory));
        store.AddOutfit(new Outfit(
            Guid.NewGuid(),
            "user-a",
            "city walk",
            new[]
            {
                new OutfitItem(top.Id, top.Name, top.Category, top.BodyZone, top.ThumbnailUrl),
                new OutfitItem(legacyHat.Id, legacyHat.Name, legacyHat.Category, legacyHat.BodyZone, legacyHat.ThumbnailUrl)
            },
            Array.Empty<string>(),
            Array.Empty<string>(),
            false,
            false,
            null,
            null,
            DateTimeOffset.UtcNow));

        // Rewrite the persisted snapshot as if it predated the Hat removal: the placeholder
        // garment (and its outfit item) becomes a legacy Hat record. BodyZone strings are not
        // touched because only the "Category" property is rewritten.
        var json = File.ReadAllText(snapshotPath).Replace("\"Category\": \"Accessory\"", "\"Category\": \"Hat\"");
        AssertTrue(json.Contains("\"Hat\"", StringComparison.Ordinal), "test setup should embed a legacy Hat record.");
        File.WriteAllText(snapshotPath, json);

        var reloaded = new FileBackedOutfitStore(snapshotPath);
        var garments = reloaded.ListGarmentsByUser("user-a");
        AssertEqual(1, garments.Count, "legacy Hat garments should be dropped on snapshot load.");
        AssertEqual(GarmentCategory.Top, garments[0].Category, "non-hat garments should survive the snapshot load.");

        var outfits = reloaded.ListOutfitsByUser("user-a");
        AssertEqual(1, outfits.Count, "outfits should survive the snapshot load.");
        AssertEqual(1, outfits[0].Items.Count, "legacy Hat outfit items should be dropped on snapshot load.");
        AssertEqual(top.Id, outfits[0].Items[0].GarmentId, "the remaining outfit item should be the non-hat garment.");
    }
    finally
    {
        if (Directory.Exists(tempPath))
        {
            Directory.Delete(tempPath, recursive: true);
        }
    }
}

static void TestApiUsesLocalFileStoreWithoutPostgres()
{
    var program = File.ReadAllText(Path.Combine("outfit_planner_back", "src", "OutfitPlanner.Api", "Program.cs"));

    AssertTrue(program.Contains("\"LocalFile\"", StringComparison.Ordinal), "empty Postgres configuration should use a durable local file store label.");
    AssertTrue(program.Contains("FileBackedOutfitStore", StringComparison.Ordinal), "API should register the durable local file store when Postgres is not configured.");
    AssertTrue(program.Contains("CreateLocalOutfitStore", StringComparison.Ordinal), "API should centralize local file store creation.");
    AssertTrue(program.Contains("Storage:Local:DataPath", StringComparison.Ordinal), "local file store path should be configurable.");
}

static void TestApiFailsClosedWithoutObjectStorageSigningSecret()
{
    var program = File.ReadAllText(Path.Combine("outfit_planner_back", "src", "OutfitPlanner.Api", "Program.cs"));

    AssertTrue(program.Contains("EnsureLocalObjectStorageSigningSecret", StringComparison.Ordinal), "API should validate the local object-storage signing secret at startup.");
    AssertTrue(program.Contains("ObjectStorage:Local:SigningSecret must be configured outside Development", StringComparison.Ordinal), "API must fail fast outside Development when the local object-storage signing secret is missing, so signed URLs cannot be forged with the source-visible development key.");
}

static void TestApiMapsValidationExceptionsToBadRequest()
{
    var program = File.ReadAllText(Path.Combine("outfit_planner_back", "src", "OutfitPlanner.Api", "Program.cs"));
    var validationException = File.ReadAllText(Path.Combine("outfit_planner_back", "src", "OutfitPlanner.Domain", "ValidationException.cs"));
    var wardrobeService = File.ReadAllText(Path.Combine("outfit_planner_back", "src", "OutfitPlanner.Application", "Services", "WardrobeService.cs"));

    AssertTrue(validationException.Contains("class ValidationException : InvalidOperationException", StringComparison.Ordinal), "ValidationException should derive from InvalidOperationException for backward compatibility.");
    AssertTrue(wardrobeService.Contains("throw new ValidationException(", StringComparison.Ordinal), "services should raise ValidationException for invalid input.");
    AssertTrue(program.Contains("catch (ValidationException ex)", StringComparison.Ordinal), "handlers should map ValidationException to 400 rather than catching all InvalidOperationException.");
    AssertTrue(program.Contains("is OutfitPlanner.Domain.ValidationException", StringComparison.Ordinal), "the global handler should centrally map uncaught ValidationException to 400 without logging it as a server fault.");
    AssertTrue(program.Contains("EnsureProviderConfiguration", StringComparison.Ordinal), "startup should validate selected provider credentials.");
    AssertTrue(program.Contains("Fashn:ApiKey must be configured", StringComparison.Ordinal), "selecting a FASHN provider without an API key should fail fast at startup.");
}

static void TestValidationFailuresRaiseValidationException()
{
    var store = new InMemoryOutfitStore();
    var service = new WardrobeService(store, store, new SystemClock());

    AssertThrows<ValidationException>(
        () => service.CreateGarment(new CreateGarmentCommand("user-a", "tee", GarmentCategory.Top, "https://app.test/x.png", "https://app.test/x.png", Array.Empty<string>(), FormalityScore: 9)),
        "an out-of-range score should raise a ValidationException (mapped to HTTP 400).");
}

static void TestApiHardensAbuseAndSecuritySurfaces()
{
    var program = File.ReadAllText(Path.Combine("outfit_planner_back", "src", "OutfitPlanner.Api", "Program.cs"));
    var authService = File.ReadAllText(Path.Combine("outfit_planner_back", "src", "OutfitPlanner.Application", "Services", "AuthService.cs"));

    AssertTrue(program.Contains("RejectionStatusCode = StatusCodes.Status429TooManyRequests", StringComparison.Ordinal), "throttled requests should return HTTP 429, not the default 503.");
    AssertTrue(program.Contains("MetadataName.RetryAfter", StringComparison.Ordinal), "rate-limit rejections should emit a Retry-After header.");
    AssertTrue(program.Contains("try-on-rate-limit", StringComparison.Ordinal), "the paid AI try-on start endpoint should be rate limited.");
    AssertTrue(program.Contains("X-Content-Type-Options", StringComparison.Ordinal) && program.Contains("nosniff", StringComparison.Ordinal), "responses should set X-Content-Type-Options: nosniff.");
    AssertTrue(program.Contains("Referrer-Policy", StringComparison.Ordinal), "responses should set a Referrer-Policy.");
    AssertTrue(program.Contains("app.UseHsts()", StringComparison.Ordinal), "non-development should enable HSTS.");
    AssertTrue(program.Contains("Cors:AllowedOrigins", StringComparison.Ordinal), "production CORS origins should be configurable rather than hardcoded to localhost.");
    AssertTrue(authService.Contains("CryptographicOperations.FixedTimeEquals", StringComparison.Ordinal), "CSRF token-hash comparison should be constant-time.");
}

static void TestPostgresSchemaContainsRepositoryTables()
{
    var schemaPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "database", "schema.sql"));
    var schema = File.ReadAllText(schemaPath);

    foreach (var table in PostgresOutfitStore.RequiredTables)
    {
        AssertTrue(schema.Contains($"create table if not exists {table}", StringComparison.OrdinalIgnoreCase), $"schema should create {table}");
    }
}

static void TestPostgresSchemaContainsAuthTables()
{
    var schemaPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "database", "schema.sql"));
    var schema = File.ReadAllText(schemaPath);

    AssertTrue(schema.Contains("normalized_email", StringComparison.OrdinalIgnoreCase), "users table should store normalized email for unique login lookup.");
    AssertTrue(schema.Contains("password_hash", StringComparison.OrdinalIgnoreCase), "users table should store password hashes, not plaintext passwords.");
    AssertTrue(schema.Contains("create table if not exists auth_external_logins", StringComparison.OrdinalIgnoreCase), "schema should store external provider account links.");
    AssertTrue(schema.Contains("create table if not exists auth_sessions", StringComparison.OrdinalIgnoreCase), "schema should store revocable server-side sessions.");
    AssertTrue(schema.Contains("token_hash", StringComparison.OrdinalIgnoreCase), "sessions should store a token hash, not raw cookie tokens.");
    AssertTrue(schema.Contains("csrf_token_hash", StringComparison.OrdinalIgnoreCase), "sessions should bind a CSRF token hash to the session.");
    AssertTrue(schema.Contains("unique (provider, provider_subject)", StringComparison.OrdinalIgnoreCase), "external logins should be unique per provider subject.");
}

static void TestPostgresSchemaContainsUserAccountProfileFields()
{
    var schemaPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "database", "schema.sql"));
    var schema = File.ReadAllText(schemaPath);

    foreach (var field in new[] { "avatar_url", "avatar_object_key", "gender" })
    {
        AssertTrue(schema.Contains(field, StringComparison.OrdinalIgnoreCase), $"users table should store {field}.");
    }

    AssertTrue(schema.Contains("gender in ('Male', 'Female')", StringComparison.OrdinalIgnoreCase), "schema should constrain gender to male or female.");
}

static void TestAuthServiceRegistersEmailUsers()
{
    var store = new InMemoryOutfitStore();
    var auth = new AuthService(store, new TestPasswordHasher(), new TestAuthTokenService(), new SystemClock(), TestRolePinning());

    var result = auth.RegisterWithPassword("Ada@Example.COM", "abc12345", "abc12345");

    AssertEqual("ada@example.com", result.User.Email, "registration should normalize email addresses.");
    AssertTrue(result.User.Id.StartsWith("usr_", StringComparison.Ordinal), "registered users should receive opaque user ids.");
    AssertTrue(!string.IsNullOrWhiteSpace(result.SessionToken), "registration should issue a session token.");
    AssertTrue(!string.IsNullOrWhiteSpace(result.CsrfToken), "registration should issue a CSRF token.");
    AssertTrue(store.GetUserByNormalizedEmail("ada@example.com")?.PasswordHash?.StartsWith("hashed:", StringComparison.Ordinal) == true, "passwords should be hashed before storage.");
    AssertTrue(store.GetActiveAuthSessionByTokenHash("hash:session-1", DateTimeOffset.UtcNow) is not null, "session lookup should use the token hash.");
}

static void TestAuthServiceUpdatesAccountProfile()
{
    var store = new InMemoryOutfitStore();
    var auth = new AuthService(store, new TestPasswordHasher(), new TestAuthTokenService(), new SystemClock(), TestRolePinning());
    var result = auth.RegisterWithPassword("profile@example.com", "abc12345", "abc12345");

    var profile = auth.UpdateProfile(result.User.Id, "Dmytro Bolibok", UserGender.Male);
    var avatar = auth.UpdateAvatar(result.User.Id, "/api/storage/signed/avatars/thumbnail/avatar.png", "avatars/thumbnail/avatar.png");

    AssertEqual("Dmytro Bolibok", profile.Username, "profile update should expose the changed username.");
    AssertEqual(UserGender.Male, profile.Gender, "profile update should expose the changed gender.");
    AssertEqual("/api/storage/signed/avatars/thumbnail/avatar.png", avatar.AvatarUrl, "avatar update should expose the current avatar URL.");
    var stored = store.GetUserById(result.User.Id);
    AssertEqual("Dmytro Bolibok", stored?.DisplayName, "username should be stored as the account display name.");
    AssertEqual(UserGender.Male, stored?.Gender, "gender should be persisted on the user account.");
    AssertEqual("avatars/thumbnail/avatar.png", stored?.AvatarObjectKey, "avatar object key should be persisted for signed URL refresh.");
}

static void TestAuthServicePasswordPolicy()
{
    var store = new InMemoryOutfitStore();
    var auth = new AuthService(store, new TestPasswordHasher(), new TestAuthTokenService(), new SystemClock(), TestRolePinning());

    auth.RegisterWithPassword("short-valid@example.com", "abc12345", "abc12345");

    AssertThrows<InvalidOperationException>(
        () => auth.RegisterWithPassword("short@example.com", "abc1234", "abc1234"),
        "passwords shorter than eight characters must be rejected");
    AssertThrows<InvalidOperationException>(
        () => auth.RegisterWithPassword("digits@example.com", "12345678", "12345678"),
        "passwords without a letter must be rejected");
    AssertThrows<InvalidOperationException>(
        () => auth.RegisterWithPassword("letters@example.com", "abcdefgh", "abcdefgh"),
        "passwords without a digit must be rejected");
}

static void TestAuthServiceRejectsDuplicateEmailRegistration()
{
    var store = new InMemoryOutfitStore();
    var auth = new AuthService(store, new TestPasswordHasher(), new TestAuthTokenService(), new SystemClock(), TestRolePinning());

    auth.RegisterWithPassword("ada@example.com", "abc12345", "abc12345");

    AssertThrows<InvalidOperationException>(
        () => auth.RegisterWithPassword("ADA@example.com", "abc12345", "abc12345"),
        "duplicate normalized emails must be rejected");
}

static void TestAuthServiceExternalLoginAutoRegisters()
{
    var store = new InMemoryOutfitStore();
    var auth = new AuthService(store, new TestPasswordHasher(), new TestAuthTokenService(), new SystemClock(), TestRolePinning());

    var first = auth.SignInWithExternalAccount(new ExternalSignInCommand(
        "google",
        "google-subject-1",
        "grace@example.com",
        EmailVerified: true,
        "Grace Hopper"));
    var second = auth.SignInWithExternalAccount(new ExternalSignInCommand(
        "google",
        "google-subject-1",
        "grace@example.com",
        EmailVerified: true,
        "Grace Hopper"));
    var apple = auth.SignInWithExternalAccount(new ExternalSignInCommand(
        "apple",
        "apple-subject-1",
        "grace@example.com",
        EmailVerified: true,
        "Grace Hopper"));

    AssertEqual(first.User.Id, second.User.Id, "known external accounts should sign in to the existing user.");
    AssertEqual(first.User.Id, apple.User.Id, "verified external email should link to an existing account instead of duplicating it.");
    AssertTrue(store.GetExternalLogin("google", "google-subject-1") is not null, "google external login should be stored.");
    AssertTrue(store.GetExternalLogin("apple", "apple-subject-1") is not null, "apple external login should be stored.");
}

static void TestAuthServiceRevokesSessionByHash()
{
    var store = new InMemoryOutfitStore();
    var auth = new AuthService(store, new TestPasswordHasher(), new TestAuthTokenService(), new SystemClock(), TestRolePinning());
    var result = auth.RegisterWithPassword("session@example.com", "abc12345", "abc12345");

    auth.RevokeSession(result.SessionToken);

    AssertTrue(store.GetActiveAuthSessionByTokenHash("hash:session-1", DateTimeOffset.UtcNow) is null, "revoked sessions should no longer authenticate.");
}

static void TestAuthServiceSessionHardening()
{
    var store = new InMemoryOutfitStore();
    var auth = new AuthService(store, new TestPasswordHasher(), new TestAuthTokenService(), new SystemClock(), TestRolePinning());
    var first = auth.RegisterWithPassword("sessions@example.com", "abc12345", "abc12345");
    var second = auth.SignInWithPassword("sessions@example.com", "abc12345");

    var sessions = auth.ListSessions(first.SessionToken);

    AssertEqual(2, sessions.Count, "session listing should include all active sessions for the authenticated user.");
    AssertTrue(sessions.All(session => session.RevokedAt is null), "active session listing should not include revoked sessions.");

    auth.RevokeAllSessions(first.SessionToken);

    AssertTrue(store.GetActiveAuthSessionByTokenHash("hash:session-1", DateTimeOffset.UtcNow) is null, "revoke all should revoke the first session.");
    AssertTrue(store.GetActiveAuthSessionByTokenHash("hash:session-3", DateTimeOffset.UtcNow) is null, "revoke all should revoke later sessions.");

    store.AddAuthSession(new AuthSession(
        Guid.NewGuid(),
        first.User.Id,
        "hash:expired",
        "hash:csrf-expired",
        DateTimeOffset.UtcNow.AddDays(-1),
        DateTimeOffset.UtcNow.AddDays(-31),
        null));

    AssertEqual(1, auth.CleanupExpiredSessions(), "expired session cleanup should remove persisted expired sessions.");
}

static RolePinningPolicy TestRolePinning()
{
    // Mirrors the production defaults: the two accounts whose roles are pinned by email.
    return new RolePinningPolicy(new RolePinningOptions(
        new[] { "dmytro.bolibok@gmail.com" },
        new[] { "olya.shaydur@gmail.com" }));
}

static void TestUserRolesDefaultToFreeAndPinnedEmailsOverride()
{
    var store = new InMemoryOutfitStore();
    var auth = new AuthService(store, new TestPasswordHasher(), new TestAuthTokenService(), new SystemClock(), TestRolePinning());

    var regular = auth.RegisterWithPassword("free@example.com", "abc12345", "abc12345");
    AssertEqual(UserRole.Free, regular.User.Role, "new accounts should default to the Free role.");

    var admin = auth.RegisterWithPassword("Dmytro.Bolibok@gmail.com", "abc12345", "abc12345");
    AssertEqual(UserRole.Admin, admin.User.Role, "the pinned admin email should always resolve to the Admin role.");

    var premium = auth.RegisterWithPassword("olya.shaydur@gmail.com", "abc12345", "abc12345");
    AssertEqual(UserRole.Premium, premium.User.Role, "the pinned premium email should always resolve to the Premium role.");

    // A tampered stored role must not leak through: the effective role stays pinned, and the
    // next sign-in converges the stored role back.
    var stored = store.GetUserByNormalizedEmail("dmytro.bolibok@gmail.com")
        ?? throw new InvalidOperationException("Pinned admin account was not stored.");
    store.UpdateUser(stored with { Role = UserRole.Free });

    var signedIn = auth.SignInWithPassword("dmytro.bolibok@gmail.com", "abc12345");

    AssertEqual(UserRole.Admin, signedIn.User.Role, "pinned roles should override a tampered stored role.");
    AssertEqual(UserRole.Admin, store.GetUserByNormalizedEmail("dmytro.bolibok@gmail.com")?.Role, "sign-in should converge the stored role back to the pinned role.");
}

static void TestAdminServiceListsSearchesAndCountsUsers()
{
    var store = new InMemoryOutfitStore();
    var pinning = TestRolePinning();
    var auth = new AuthService(store, new TestPasswordHasher(), new TestAuthTokenService(), new SystemClock(), pinning);
    var admin = new AdminService(store, store, pinning, new SystemClock());

    var ada = auth.RegisterWithPassword("ada@example.com", "abc12345", "abc12345");
    var grace = auth.RegisterWithPassword("grace@example.com", "abc12345", "abc12345");
    store.CreateGarment(CreateGarment(ada.User.Id, "tee", GarmentCategory.Top));
    store.CreateGarment(CreateGarment(ada.User.Id, "jeans", GarmentCategory.Bottom));
    store.AddOutfit(new Outfit(
        Guid.NewGuid(),
        ada.User.Id,
        "look",
        Array.Empty<OutfitItem>(),
        Array.Empty<string>(),
        Array.Empty<string>(),
        false,
        false,
        null,
        null,
        DateTimeOffset.UtcNow));

    var page = admin.ListUsers(null, null, 0, 10);
    AssertEqual(2, page.TotalCount, "admin listing should count all users.");
    var adaRecord = page.Items.Single(item => item.User.Id == ada.User.Id);
    AssertEqual(2, adaRecord.GarmentCount, "admin listing should count the user's garments.");
    AssertEqual(1, adaRecord.OutfitCount, "admin listing should count the user's outfits.");
    AssertTrue(adaRecord.ActiveSessionCount >= 1, "admin listing should count active sessions.");

    var search = admin.ListUsers("grace", null, 0, 10);
    AssertEqual(1, search.TotalCount, "admin search should filter by email.");
    AssertEqual(grace.User.Id, search.Items.Single().User.Id, "admin search should return the matching user.");

    store.UpdateUser((store.GetUserById(grace.User.Id)
        ?? throw new InvalidOperationException("Second account was not stored.")) with { Role = UserRole.Premium });
    var premiumOnly = admin.ListUsers(null, UserRole.Premium, 0, 10);
    AssertEqual(1, premiumOnly.TotalCount, "admin role filter should match stored roles.");
    AssertEqual(grace.User.Id, premiumOnly.Items.Single().User.Id, "admin role filter should return the premium user.");

    var paged = admin.ListUsers(null, null, 1, 1);
    AssertEqual(1, paged.Items.Count, "admin paging should respect the limit.");
    AssertEqual(2, paged.TotalCount, "admin paging should report the unpaged total.");

    var stats = admin.Stats();
    AssertEqual(2, stats.TotalUsers, "admin stats should count users.");
    AssertEqual(2, stats.TotalGarments, "admin stats should count garments.");
    AssertEqual(1, stats.TotalOutfits, "admin stats should count outfits.");
}

static void TestAdminServiceChangesRolesWithGuards()
{
    var store = new InMemoryOutfitStore();
    var pinning = TestRolePinning();
    var auth = new AuthService(store, new TestPasswordHasher(), new TestAuthTokenService(), new SystemClock(), pinning);
    var admin = new AdminService(store, store, pinning, new SystemClock());

    var actingAdmin = auth.RegisterWithPassword("dmytro.bolibok@gmail.com", "abc12345", "abc12345");
    var member = auth.RegisterWithPassword("member@example.com", "abc12345", "abc12345");
    var pinnedPremium = auth.RegisterWithPassword("olya.shaydur@gmail.com", "abc12345", "abc12345");

    var updated = admin.ChangeRole(actingAdmin.User.Id, member.User.Id, UserRole.Premium);
    AssertEqual(UserRole.Premium, updated?.User.Role, "admin role changes should return the updated record.");
    AssertEqual(UserRole.Premium, store.GetUserById(member.User.Id)?.Role, "admin role changes should persist the stored role.");

    AssertTrue(admin.ChangeRole(actingAdmin.User.Id, "usr_missing", UserRole.Premium) is null, "changing a missing user's role should report not found.");
    AssertThrows<InvalidOperationException>(
        () => admin.ChangeRole(actingAdmin.User.Id, actingAdmin.User.Id, UserRole.Free),
        "admins must not change their own role");
    AssertThrows<InvalidOperationException>(
        () => admin.ChangeRole(actingAdmin.User.Id, pinnedPremium.User.Id, UserRole.Free),
        "pinned account roles must not be changeable");
    var pinnedRecord = admin.GetUser(pinnedPremium.User.Id);
    AssertTrue(pinnedRecord is not null && admin.EffectiveRole(pinnedRecord.User) == UserRole.Premium, "the pinned premium account should keep its role.");
}

static void TestAdminServiceDeleteGuards()
{
    var store = new InMemoryOutfitStore();
    var pinning = TestRolePinning();
    var auth = new AuthService(store, new TestPasswordHasher(), new TestAuthTokenService(), new SystemClock(), pinning);
    var admin = new AdminService(store, store, pinning, new SystemClock());

    var actingAdmin = auth.RegisterWithPassword("dmytro.bolibok@gmail.com", "abc12345", "abc12345");
    var member = auth.RegisterWithPassword("member@example.com", "abc12345", "abc12345");
    var pinnedPremium = auth.RegisterWithPassword("olya.shaydur@gmail.com", "abc12345", "abc12345");

    AssertThrows<InvalidOperationException>(
        () => admin.RequireDeletableUser(actingAdmin.User.Id, actingAdmin.User.Id),
        "admins must not delete their own account from the admin panel");
    AssertThrows<InvalidOperationException>(
        () => admin.RequireDeletableUser(actingAdmin.User.Id, pinnedPremium.User.Id),
        "pinned accounts must not be deletable from the admin panel");
    AssertTrue(admin.RequireDeletableUser(actingAdmin.User.Id, member.User.Id) is not null, "regular accounts should be deletable by an admin.");
    AssertTrue(admin.RequireDeletableUser(actingAdmin.User.Id, "usr_missing") is null, "deleting a missing user should report not found.");
}

static void TestPostgresSchemaContainsAccountRoles()
{
    var schemaPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "database", "schema.sql"));
    var schema = File.ReadAllText(schemaPath);

    AssertTrue(schema.Contains("role text not null default 'Free'", StringComparison.OrdinalIgnoreCase), "users table should store the account role with a Free default.");
    AssertTrue(schema.Contains("role in ('Free', 'Premium', 'Admin')", StringComparison.OrdinalIgnoreCase), "schema should constrain roles to Free, Premium, or Admin.");

    var migrationPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "database", "migrations", "011_account_roles.sql"));
    var migration = File.ReadAllText(migrationPath);
    AssertTrue(migration.Contains("dmytro.bolibok@gmail.com", StringComparison.OrdinalIgnoreCase), "migration should backfill the pinned admin account role.");
    AssertTrue(migration.Contains("olya.shaydur@gmail.com", StringComparison.OrdinalIgnoreCase), "migration should backfill the pinned premium account role.");
}

static void TestApiExposesAdminEndpoints()
{
    var rootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var program = File.ReadAllText(Path.Combine(rootPath, "src", "OutfitPlanner.Api", "Program.cs"));
    var contracts = File.ReadAllText(Path.Combine(rootPath, "src", "OutfitPlanner.Api", "Contracts", "ApiContracts.cs"));

    foreach (var route in new[]
    {
        "MapGet(\"/admin/stats\"",
        "MapGet(\"/admin/users\"",
        "MapGet(\"/admin/users/{userId}\"",
        "MapPut(\"/admin/users/{userId}/role\"",
        "MapPost(\"/admin/users/{userId}/sessions/revoke\"",
        "MapPost(\"/admin/users/{userId}/purge-ai-outputs\"",
        "MapGet(\"/admin/users/{userId}/export\"",
        "MapDelete(\"/admin/users/{userId}\""
    })
    {
        AssertTrue(program.Contains(route, StringComparison.Ordinal), $"api should expose {route}.");
    }

    AssertTrue(program.Contains("RequireAdmin(context)", StringComparison.Ordinal), "admin endpoints should be gated by the admin role.");
    AssertTrue(program.Contains("CurrentUserRoleItemKey", StringComparison.Ordinal), "the session middleware should resolve the current user's role.");
    AssertTrue(contracts.Contains("UserRole Role", StringComparison.Ordinal), "auth and admin responses should expose the account role.");
    AssertTrue(contracts.Contains("bool RolePinned", StringComparison.Ordinal), "admin responses should mark pinned accounts.");
}

static void TestCreditLedgerGrantsDebitsAndRefunds()
{
    var store = new InMemoryOutfitStore();
    var pinning = TestRolePinning();
    var clock = new SystemClock();
    var credits = new CreditLedgerService(store, PlanCatalog.Default, pinning, clock);
    var auth = new AuthService(store, new TestPasswordHasher(), new TestAuthTokenService(), clock, pinning);

    var freeUser = store.GetUserById(auth.RegisterWithPassword("credits-free@example.com", "abc12345", "abc12345").User.Id)
        ?? throw new InvalidOperationException("Free account was not stored.");
    AssertEqual(8, credits.GetBalance(freeUser).Balance, "free accounts should receive the one-time trial grant on first read.");
    AssertEqual(8, credits.GetBalance(freeUser).Balance, "the trial grant must not be duplicated.");

    var jobId = Guid.NewGuid();
    credits.DebitForJob(freeUser, jobId, 2);
    AssertEqual(6, credits.GetBalance(freeUser).Balance, "debits should reduce the balance.");
    AssertThrows<InvalidOperationException>(
        () => credits.DebitForJob(freeUser, Guid.NewGuid(), 100),
        "insufficient balance must reject the debit");

    credits.RefundJob(freeUser.Id, jobId);
    credits.RefundJob(freeUser.Id, jobId);
    AssertEqual(8, credits.GetBalance(freeUser).Balance, "a failed job should be refunded exactly once.");

    var premiumUser = store.GetUserById(auth.RegisterWithPassword("olya.shaydur@gmail.com", "abc12345", "abc12345").User.Id)
        ?? throw new InvalidOperationException("Premium account was not stored.");
    AssertEqual(100, credits.GetBalance(premiumUser).Balance, "premium accounts should receive the monthly allowance.");
    AssertEqual(100, credits.GetBalance(premiumUser).Balance, "the monthly grant must not be duplicated within the month.");

    var adminUser = store.GetUserById(auth.RegisterWithPassword("dmytro.bolibok@gmail.com", "abc12345", "abc12345").User.Id)
        ?? throw new InvalidOperationException("Admin account was not stored.");
    AssertTrue(credits.GetBalance(adminUser).Unlimited, "admin accounts should have unlimited credits.");
    AssertEqual(0, store.ListCreditEntriesByUser(adminUser.Id).Count, "admin accounts should not receive ledger grants.");
    credits.DebitForJob(adminUser, Guid.NewGuid(), 5);
    AssertEqual(0, store.ListCreditEntriesByUser(adminUser.Id).Count, "admin debits should be a no-op.");

    AssertEqual(12, credits.AdminAdjust(freeUser, 4), "admin adjustments should apply to the balance.");
    AssertThrows<InvalidOperationException>(() => credits.AdminAdjust(freeUser, 0), "zero adjustments must be rejected");
}

static void TestTrialGrantTopsUpToConfiguredAmount()
{
    var store = new InMemoryOutfitStore();
    var pinning = TestRolePinning();
    var clock = new SystemClock();
    var auth = new AuthService(store, new TestPasswordHasher(), new TestAuthTokenService(), clock, pinning);
    var user = store.GetUserById(auth.RegisterWithPassword("topup-trial@example.com", "abc12345", "abc12345").User.Id)
        ?? throw new InvalidOperationException("Account was not stored.");

    // Simulate an account granted under the old 6-credit trial.
    store.AddCreditEntry(new CreditLedgerEntry(Guid.NewGuid(), user.Id, 6, CreditLedgerReason.TrialGrant, null, null, clock.UtcNow));

    var credits = new CreditLedgerService(store, PlanCatalog.Default, pinning, clock);
    AssertEqual(8, credits.GetBalance(user).Balance, "existing trial accounts should be topped up to the configured amount.");
    AssertEqual(8, credits.GetBalance(user).Balance, "the top-up must not repeat.");
    AssertEqual(2, store.ListCreditEntriesByUser(user.Id).Count, "the top-up should append exactly one extra grant entry.");

    // Lowering the config must never claw back already granted credits.
    var lowered = new PlanCatalog(
        PlanCatalog.Default.For(UserRole.Free) with { TrialCredits = 4 },
        PlanCatalog.Default.For(UserRole.Premium),
        PlanCatalog.Default.For(UserRole.Admin));
    var loweredCredits = new CreditLedgerService(store, lowered, pinning, clock);
    AssertEqual(8, loweredCredits.GetBalance(user).Balance, "a lowered trial config must not claw back granted credits.");
}

static void TestBillingRulesMapStatuses()
{
    foreach (var premium in new[] { "active", "trialing", "past_due", " Active " })
    {
        AssertTrue(BillingRules.GrantsPremium(premium), $"status '{premium}' should grant premium.");
    }

    foreach (var free in new[] { "canceled", "unpaid", "incomplete", "incomplete_expired", "paused", "", null })
    {
        AssertTrue(!BillingRules.GrantsPremium(free), $"status '{free}' must not grant premium.");
    }
}

static BillingOptions TestBillingOptions()
{
    return new BillingOptions(
        PremiumPriceId: "price_premium",
        PremiumDisplayPrice: "$9/mo",
        TopUpPacks: new[] { new BillingTopUpPack("pack-20", 20, "price_pack20", "$5") },
        CheckoutSuccessUrl: "https://app.example/upgrade?checkout=success",
        CheckoutCancelUrl: "https://app.example/upgrade?checkout=cancelled",
        PortalReturnUrl: "https://app.example/upgrade");
}

static void TestBillingServiceGatesCheckoutAndPortal()
{
    var store = new InMemoryOutfitStore();
    var pinning = TestRolePinning();
    var clock = new SystemClock();
    var credits = new CreditLedgerService(store, PlanCatalog.Default, pinning, clock);
    var auth = new AuthService(store, new TestPasswordHasher(), new TestAuthTokenService(), clock, pinning);
    var provider = new FakeBillingProvider();
    var billing = new BillingService(store, store, store, credits, provider, TestBillingOptions(), pinning, clock);

    var free = store.GetUserById(auth.RegisterWithPassword("billing-free@example.com", "abc12345", "abc12345").User.Id)
        ?? throw new InvalidOperationException("Free account was not stored.");
    var premium = store.GetUserById(auth.RegisterWithPassword("olya.shaydur@gmail.com", "abc12345", "abc12345").User.Id)
        ?? throw new InvalidOperationException("Premium account was not stored.");
    var admin = store.GetUserById(auth.RegisterWithPassword("dmytro.bolibok@gmail.com", "abc12345", "abc12345").User.Id)
        ?? throw new InvalidOperationException("Admin account was not stored.");

    AssertEqual("https://billing.example/checkout", billing.StartSubscriptionCheckoutAsync(free.Id, CancellationToken.None).GetAwaiter().GetResult(),
        "free accounts should get a subscription checkout url.");
    AssertThrows<InvalidOperationException>(
        () => billing.StartSubscriptionCheckoutAsync(premium.Id, CancellationToken.None).GetAwaiter().GetResult(),
        "premium accounts must not start a second subscription checkout");
    AssertThrows<InvalidOperationException>(
        () => billing.StartSubscriptionCheckoutAsync(admin.Id, CancellationToken.None).GetAwaiter().GetResult(),
        "admin accounts are not sellable");

    AssertThrows<InvalidOperationException>(
        () => billing.StartTopUpCheckoutAsync(free.Id, "pack-20", CancellationToken.None).GetAwaiter().GetResult(),
        "top-ups are a premium feature");
    AssertEqual("https://billing.example/topup/pack-20", billing.StartTopUpCheckoutAsync(premium.Id, "pack-20", CancellationToken.None).GetAwaiter().GetResult(),
        "premium accounts should get a top-up checkout url.");
    AssertThrows<InvalidOperationException>(
        () => billing.StartTopUpCheckoutAsync(premium.Id, "missing", CancellationToken.None).GetAwaiter().GetResult(),
        "unknown packs must be rejected");

    AssertThrows<InvalidOperationException>(
        () => billing.CreatePortalAsync(free.Id, CancellationToken.None).GetAwaiter().GetResult(),
        "portal requires an existing subscription");

    var status = billing.GetStatus(free.Id);
    AssertTrue(status.Enabled && status.SubscriptionPriceConfigured, "billing status should reflect the configured provider.");
    AssertEqual(1, status.TopUpPacks.Count, "configured packs should be offered.");
    AssertTrue(status.Subscription is null && !status.PortalAvailable, "accounts without subscriptions have no portal.");

    var disabledProvider = new FakeBillingProvider { Enabled = false };
    var disabled = new BillingService(store, store, store, credits, disabledProvider, BillingOptions.Empty, pinning, clock);
    AssertTrue(!disabled.GetStatus(free.Id).Enabled, "disabled providers must read as disabled.");
    AssertThrows<InvalidOperationException>(
        () => disabled.StartSubscriptionCheckoutAsync(free.Id, CancellationToken.None).GetAwaiter().GetResult(),
        "checkout must be rejected while billing is disabled");
}

static void TestBillingWebhooksDriveRolesAndTopUps()
{
    var store = new InMemoryOutfitStore();
    var pinning = TestRolePinning();
    var clock = new SystemClock();
    var credits = new CreditLedgerService(store, PlanCatalog.Default, pinning, clock);
    var auth = new AuthService(store, new TestPasswordHasher(), new TestAuthTokenService(), clock, pinning);
    var provider = new FakeBillingProvider();
    var billing = new BillingService(store, store, store, credits, provider, TestBillingOptions(), pinning, clock);

    var user = store.GetUserById(auth.RegisterWithPassword("webhook-user@example.com", "abc12345", "abc12345").User.Id)
        ?? throw new InvalidOperationException("Account was not stored.");

    provider.NextEvent = new BillingWebhookEvent("evt-1", BillingWebhookEventKind.CheckoutCompleted,
        UserId: user.Id, CustomerId: "cus_1", SubscriptionId: "sub_1", CheckoutMode: "subscription");
    AssertEqual("processed", billing.HandleWebhookAsync("{}", "valid", CancellationToken.None).GetAwaiter().GetResult().Status,
        "subscription checkout completion should process.");
    AssertEqual(UserRole.Premium, store.GetUserById(user.Id)?.Role, "checkout completion should promote the stored role.");
    AssertEqual("sub_1", store.GetSubscriptionByUser(user.Id)?.ExternalSubscriptionId, "the subscription row should be bound to the user.");

    AssertEqual("duplicate", billing.HandleWebhookAsync("{}", "valid", CancellationToken.None).GetAwaiter().GetResult().Status,
        "replayed event ids must be no-ops.");

    provider.NextEvent = new BillingWebhookEvent("evt-2", BillingWebhookEventKind.SubscriptionUpdated,
        SubscriptionId: "sub_1", Status: "past_due", CurrentPeriodEnd: clock.UtcNow.AddDays(30));
    billing.HandleWebhookAsync("{}", "valid", CancellationToken.None).GetAwaiter().GetResult();
    AssertEqual(UserRole.Premium, store.GetUserById(user.Id)?.Role, "past_due keeps premium as a grace window.");
    AssertEqual("past_due", store.GetSubscriptionByUser(user.Id)?.Status, "subscription status should update by external id lookup.");

    provider.NextEvent = new BillingWebhookEvent("evt-3", BillingWebhookEventKind.SubscriptionDeleted, SubscriptionId: "sub_1");
    billing.HandleWebhookAsync("{}", "valid", CancellationToken.None).GetAwaiter().GetResult();
    AssertEqual(UserRole.Free, store.GetUserById(user.Id)?.Role, "deleted subscriptions should demote the stored role.");
    AssertEqual("canceled", store.GetSubscriptionByUser(user.Id)?.Status, "deleted subscriptions should read canceled.");

    var refreshedUser = store.GetUserById(user.Id) ?? throw new InvalidOperationException("Account disappeared.");
    var balanceBefore = credits.GetBalance(refreshedUser).Balance;
    provider.NextEvent = new BillingWebhookEvent("evt-4", BillingWebhookEventKind.CheckoutCompleted,
        UserId: user.Id, CheckoutMode: "payment", TopUpPackId: "pack-20", TopUpCredits: 20);
    billing.HandleWebhookAsync("{}", "valid", CancellationToken.None).GetAwaiter().GetResult();
    AssertEqual(balanceBefore + 20, credits.GetBalance(refreshedUser).Balance, "top-up checkouts should grant credits.");

    // Pinned accounts are exempt from webhook-driven role changes.
    var pinnedPremium = store.GetUserById(auth.RegisterWithPassword("olya.shaydur@gmail.com", "abc12345", "abc12345").User.Id)
        ?? throw new InvalidOperationException("Pinned account was not stored.");
    provider.NextEvent = new BillingWebhookEvent("evt-5", BillingWebhookEventKind.CheckoutCompleted,
        UserId: pinnedPremium.Id, CustomerId: "cus_2", SubscriptionId: "sub_2", CheckoutMode: "subscription");
    billing.HandleWebhookAsync("{}", "valid", CancellationToken.None).GetAwaiter().GetResult();
    provider.NextEvent = new BillingWebhookEvent("evt-6", BillingWebhookEventKind.SubscriptionDeleted, SubscriptionId: "sub_2");
    billing.HandleWebhookAsync("{}", "valid", CancellationToken.None).GetAwaiter().GetResult();
    AssertEqual(UserRole.Premium, store.GetUserById(pinnedPremium.Id)?.Role, "pinned stored roles must not be rewritten by webhooks.");

    AssertThrows<InvalidOperationException>(
        () => billing.HandleWebhookAsync("{}", "bogus", CancellationToken.None).GetAwaiter().GetResult(),
        "invalid signatures must be rejected");

    provider.NextEvent = new BillingWebhookEvent("evt-7", BillingWebhookEventKind.SubscriptionUpdated, SubscriptionId: "sub_unknown", Status: "active");
    AssertEqual("ignored", billing.HandleWebhookAsync("{}", "valid", CancellationToken.None).GetAwaiter().GetResult().Status,
        "unresolvable subscriptions are ignored, not errors.");
}

static void TestLocalFileStorePersistsBillingRecords()
{
    var tempPath = Path.Combine(Path.GetTempPath(), "outfit-planner-local-store-tests", Guid.NewGuid().ToString("N"));

    try
    {
        var snapshotPath = Path.Combine(tempPath, "outfit-store.json");
        var now = DateTimeOffset.UtcNow;
        var user = new UserAccount(
            "usr_billing",
            "billing@example.com",
            "billing@example.com",
            "Billing User",
            "hashed-password",
            now,
            now,
            null);

        var first = new FileBackedOutfitStore(snapshotPath);
        first.AddUser(user);
        first.UpsertSubscription(new BillingSubscription(user.Id, "stripe", "cus_1", "sub_1", "active", now.AddDays(30), now));
        AssertTrue(first.TryRecordBillingEvent("evt-1", now), "the first webhook event record should win.");

        var second = new FileBackedOutfitStore(snapshotPath);
        var subscription = second.GetSubscriptionByUser(user.Id) ?? throw new InvalidOperationException("Subscription did not persist.");
        AssertEqual("active", subscription.Status, "the subscription status should round-trip the snapshot.");
        AssertEqual("cus_1", subscription.ExternalCustomerId, "the customer id should round-trip the snapshot.");
        AssertEqual("sub_1", second.GetSubscriptionByExternalSubscriptionId("sub_1")?.ExternalSubscriptionId, "the external subscription lookup should round-trip.");
        AssertTrue(!second.TryRecordBillingEvent("evt-1", now), "processed webhook events must stay processed across restarts.");
    }
    finally
    {
        if (Directory.Exists(tempPath))
        {
            Directory.Delete(tempPath, recursive: true);
        }
    }
}

static void TestPostgresSchemaContainsBillingTables()
{
    var schemaPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "database", "schema.sql"));
    var schema = File.ReadAllText(schemaPath);
    AssertTrue(schema.Contains("create table if not exists billing_subscriptions", StringComparison.OrdinalIgnoreCase), "schema should declare the billing subscriptions table.");
    AssertTrue(schema.Contains("create table if not exists billing_webhook_events", StringComparison.OrdinalIgnoreCase), "schema should declare the billing webhook idempotency table.");
    AssertTrue(schema.Contains("ux_billing_subscriptions_external_subscription", StringComparison.OrdinalIgnoreCase), "the external subscription id should be unique.");

    var migrationPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "database", "migrations", "013_billing.sql"));
    AssertTrue(File.Exists(migrationPath), "migration 013 should create the billing tables.");
}

static void TestEntitlementCapsBlockCreation()
{
    var store = new InMemoryOutfitStore();
    var pinning = TestRolePinning();
    var clock = new SystemClock();
    var catalog = new PlanCatalog(
        PlanCatalog.Default.For(UserRole.Free) with { MaxGarments = 1, MaxOutfits = 1, MaxBodyReferencePhotos = 1 },
        PlanCatalog.Default.For(UserRole.Premium),
        PlanCatalog.Default.For(UserRole.Admin));
    var credits = new CreditLedgerService(store, catalog, pinning, clock);
    var entitlements = new EntitlementService(store, store, store, store, catalog, pinning, credits);
    var auth = new AuthService(store, new TestPasswordHasher(), new TestAuthTokenService(), clock, pinning);
    var wardrobe = new WardrobeService(store, store, clock, entitlements: entitlements);
    var outfits = new OutfitService(store, store, clock, entitlements: entitlements);

    var free = auth.RegisterWithPassword("caps@example.com", "abc12345", "abc12345");
    var garment = wardrobe.CreateGarment(CreateGarment(free.User.Id, "tee", GarmentCategory.Top));
    AssertThrows<InvalidOperationException>(
        () => wardrobe.CreateGarment(CreateGarment(free.User.Id, "second tee", GarmentCategory.Top)),
        "the garment cap must block creation at the plan limit");

    outfits.CreateOutfit(free.User.Id, "look", new[] { garment.Id });
    AssertThrows<InvalidOperationException>(
        () => outfits.CreateOutfit(free.User.Id, "look-2", new[] { garment.Id }),
        "the outfit cap must block creation at the plan limit");

    wardrobe.CreateBodyReferencePhoto(free.User.Id, "https://example.com/body.png");
    AssertThrows<InvalidOperationException>(
        () => wardrobe.CreateBodyReferencePhoto(free.User.Id, "https://example.com/body-2.png"),
        "the body reference photo cap must block creation at the plan limit");

    // The pinned premium account has no garment/outfit caps.
    var premium = auth.RegisterWithPassword("olya.shaydur@gmail.com", "abc12345", "abc12345");
    wardrobe.CreateGarment(CreateGarment(premium.User.Id, "premium tee", GarmentCategory.Top));
    wardrobe.CreateGarment(CreateGarment(premium.User.Id, "premium jeans", GarmentCategory.Bottom));
    AssertEqual(2, store.ListGarmentsByUser(premium.User.Id).Count, "premium accounts should not hit the free garment cap.");
}

static void TestTryOnEstimateAppliesPlanModesAndResolution()
{
    var store = new InMemoryOutfitStore();
    var pinning = TestRolePinning();
    var clock = new SystemClock();
    var catalog = PlanCatalog.Default;
    var credits = new CreditLedgerService(store, catalog, pinning, clock);
    var entitlements = new EntitlementService(store, store, store, store, catalog, pinning, credits);
    var auth = new AuthService(store, new TestPasswordHasher(), new TestAuthTokenService(), clock, pinning);

    var free = auth.RegisterWithPassword("tier-free@example.com", "abc12345", "abc12345");
    var premium = auth.RegisterWithPassword("olya.shaydur@gmail.com", "abc12345", "abc12345");
    auth.UpdateProfile(free.User.Id, "Free User", UserGender.Female);
    auth.UpdateProfile(premium.User.Id, "Olya", UserGender.Female);

    var outfitService = new OutfitService(store, store, clock);
    var freeTop = store.CreateGarment(CreateGarment(free.User.Id, "tee", GarmentCategory.Top));
    var freeBottom = store.CreateGarment(CreateGarment(free.User.Id, "jeans", GarmentCategory.Bottom));
    var freePair = outfitService.CreateOutfit(free.User.Id, "casual", new[] { freeTop.Id, freeBottom.Id });
    var freeSingle = outfitService.CreateOutfit(free.User.Id, "one piece", new[] { freeTop.Id });
    var premiumTop = store.CreateGarment(CreateGarment(premium.User.Id, "silk top", GarmentCategory.Top));
    var premiumBottom = store.CreateGarment(CreateGarment(premium.User.Id, "skirt", GarmentCategory.Bottom));
    var premiumPair = outfitService.CreateOutfit(premium.User.Id, "evening", new[] { premiumTop.Id, premiumBottom.Id });

    // FASHN tryon-max quality configured at 4k: 5 credits per run, repriced to 2 under a 1k cap.
    var provider = new FashnTryOnProvider(new HttpClient(), new FashnTryOnSettings(
        "test-key", "tryon-max", "quality", 1, TimeSpan.Zero, 1, "png", false, true, "auto", null, Resolution: "4k"));
    AssertEqual(5, provider.Capabilities.CreditsPerRun, "the 4k configuration should price 5 credits per run.");
    AssertEqual(2, provider.CapabilitiesFor("1k").CreditsPerRun, "the 1k cap should reprice to 2 credits per run.");
    AssertTrue(provider.CapabilitiesFor("1k").SettingsHash != provider.Capabilities.SettingsHash, "the 1k cap must change the settings hash so tiers cache separately.");

    var service = new TryOnService(store, store, store, new RecordingTryOnJobQueue(), provider, new TryOnCostEstimator(), clock, entitlements: entitlements, credits: credits);

    var gated = service.Estimate(free.User.Id, freePair.Id, TryOnMode.SequentialOutfitTryOn, "https://example.com/p.jpg", null);
    AssertTrue(!gated.IsAvailable, "sequential try-on must be unavailable on the free plan.");
    AssertTrue(gated.RequiresUpgrade, "the plan gate should be marked as an upgrade opportunity.");

    var freeEstimate = service.Estimate(free.User.Id, freeSingle.Id, TryOnMode.SingleGarmentTryOn, "https://example.com/p.jpg", null);
    AssertTrue(freeEstimate.IsAvailable, "single garment try-on should stay available on the free plan.");
    AssertEqual(2, freeEstimate.EstimatedCredits, "free accounts should be priced at the 1k resolution cap.");

    var premiumEstimate = service.Estimate(premium.User.Id, premiumPair.Id, TryOnMode.SequentialOutfitTryOn, "https://example.com/p.jpg", null);
    AssertTrue(premiumEstimate.IsAvailable && !premiumEstimate.RequiresUpgrade, "sequential try-on should be available on the premium plan.");
    AssertEqual(10, premiumEstimate.EstimatedCredits, "premium accounts should be priced at the configured 4k resolution per garment run.");
}

static void TestStartTryOnDebitsCreditsCacheHitsAndRefunds()
{
    var store = new InMemoryOutfitStore();
    var pinning = TestRolePinning();
    var clock = new SystemClock();
    var catalog = PlanCatalog.Default;
    var credits = new CreditLedgerService(store, catalog, pinning, clock);
    var entitlements = new EntitlementService(store, store, store, store, catalog, pinning, credits);
    var auth = new AuthService(store, new TestPasswordHasher(), new TestAuthTokenService(), clock, pinning);

    var free = auth.RegisterWithPassword("spender@example.com", "abc12345", "abc12345");
    auth.UpdateProfile(free.User.Id, "Spender", UserGender.Female);
    var freeUser = store.GetUserById(free.User.Id) ?? throw new InvalidOperationException("Account was not stored.");
    var outfitService = new OutfitService(store, store, clock);
    var top = store.CreateGarment(CreateGarment(free.User.Id, "tee", GarmentCategory.Top));
    var outfit = outfitService.CreateOutfit(free.User.Id, "casual", new[] { top.Id });

    var queue = new RecordingTryOnJobQueue();
    var provider = new CountingTryOnProvider();
    var service = new TryOnService(store, store, store, queue, provider, new TryOnCostEstimator(), clock, entitlements: entitlements, credits: credits);

    var estimate = service.Estimate(free.User.Id, outfit.Id, TryOnMode.SingleGarmentTryOn, "https://example.com/p.jpg", null);
    var job = service.StartAsync(free.User.Id, outfit.Id, "https://example.com/p.jpg", consentAccepted: true, TryOnMode.SingleGarmentTryOn, estimate.EstimatedCredits, estimate.CacheKey)
        .GetAwaiter().GetResult();
    AssertEqual(7, credits.GetBalance(freeUser).Balance, "a paid start should debit the estimated credits before queueing.");
    AssertEqual(0, queue.PriorityEnqueued.Count, "free accounts should use the normal queue.");

    service.ProcessQueuedJobAsync(job.Id).GetAwaiter().GetResult();
    AssertEqual(TryOnStatus.Succeeded, service.GetJob(free.User.Id, job.Id)?.Status, "the queued job should succeed through the provider.");
    AssertEqual(7, credits.GetBalance(freeUser).Balance, "successful jobs keep their debit.");

    var cachedEstimate = service.Estimate(free.User.Id, outfit.Id, TryOnMode.SingleGarmentTryOn, "https://example.com/p.jpg", null);
    AssertTrue(cachedEstimate.HasCachedResult, "the repeat estimate should see the cached result.");
    var cacheHit = service.StartAsync(free.User.Id, outfit.Id, "https://example.com/p.jpg", consentAccepted: true, TryOnMode.SingleGarmentTryOn, cachedEstimate.EstimatedCredits, cachedEstimate.CacheKey)
        .GetAwaiter().GetResult();
    AssertTrue(cacheHit.ServedFromCache, "the repeat start should be served from cache.");
    AssertEqual(7, credits.GetBalance(freeUser).Balance, "cache hits must not debit credits.");

    // A failing provider refunds the debit when the worker marks the job failed.
    var failingService = new TryOnService(store, store, store, queue, new ThrowingTryOnProvider(), new TryOnCostEstimator(), clock, entitlements: entitlements, credits: credits);
    var bottom = store.CreateGarment(CreateGarment(free.User.Id, "jeans", GarmentCategory.Bottom));
    var failingOutfit = outfitService.CreateOutfit(free.User.Id, "risky", new[] { bottom.Id });
    var failingEstimate = failingService.Estimate(free.User.Id, failingOutfit.Id, TryOnMode.SingleGarmentTryOn, "https://example.com/p.jpg", null);
    var failingJob = failingService.StartAsync(free.User.Id, failingOutfit.Id, "https://example.com/p.jpg", consentAccepted: true, TryOnMode.SingleGarmentTryOn, failingEstimate.EstimatedCredits, failingEstimate.CacheKey)
        .GetAwaiter().GetResult();
    AssertEqual(6, credits.GetBalance(freeUser).Balance, "the failing job should debit on start.");
    failingService.ProcessQueuedJobAsync(failingJob.Id).GetAwaiter().GetResult();
    AssertEqual(TryOnStatus.Failed, failingService.GetJob(free.User.Id, failingJob.Id)?.Status, "the provider failure should fail the job.");
    AssertEqual(7, credits.GetBalance(freeUser).Balance, "failed jobs must refund their debit.");

    // Premium jobs ride the priority queue.
    var premium = auth.RegisterWithPassword("olya.shaydur@gmail.com", "abc12345", "abc12345");
    auth.UpdateProfile(premium.User.Id, "Olya", UserGender.Female);
    var premiumTop = store.CreateGarment(CreateGarment(premium.User.Id, "silk top", GarmentCategory.Top));
    var premiumOutfit = outfitService.CreateOutfit(premium.User.Id, "evening", new[] { premiumTop.Id });
    var premiumEstimate = service.Estimate(premium.User.Id, premiumOutfit.Id, TryOnMode.SingleGarmentTryOn, "https://example.com/p.jpg", null);
    service.StartAsync(premium.User.Id, premiumOutfit.Id, "https://example.com/p.jpg", consentAccepted: true, TryOnMode.SingleGarmentTryOn, premiumEstimate.EstimatedCredits, premiumEstimate.CacheKey)
        .GetAwaiter().GetResult();
    AssertEqual(1, queue.PriorityEnqueued.Count, "premium jobs should enter the priority queue.");
}

static void TestTryOnQueuePrioritizesPremiumJobs()
{
    var queue = new InMemoryTryOnJobQueue();
    var normal = Guid.NewGuid();
    var priority = Guid.NewGuid();
    queue.EnqueueAsync(normal).AsTask().GetAwaiter().GetResult();
    queue.EnqueueAsync(priority, priority: true).AsTask().GetAwaiter().GetResult();

    AssertEqual(priority, queue.DequeueAsync(CancellationToken.None).GetAwaiter().GetResult(), "priority jobs should dequeue before earlier normal jobs.");
    AssertEqual(normal, queue.DequeueAsync(CancellationToken.None).GetAwaiter().GetResult(), "normal jobs should dequeue after the priority queue drains.");
}

static void TestPostgresSchemaContainsCreditLedger()
{
    var schemaPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "database", "schema.sql"));
    var schema = File.ReadAllText(schemaPath);
    AssertTrue(schema.Contains("create table if not exists account_credit_ledger", StringComparison.OrdinalIgnoreCase), "schema should declare the AI-credit ledger table.");
    AssertTrue(schema.Contains("'TrialGrant', 'SubscriptionGrant', 'TopUp', 'TryOnSpend', 'Refund', 'AdminAdjustment'", StringComparison.OrdinalIgnoreCase), "the ledger should constrain its reasons.");

    var migrationPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "database", "migrations", "012_account_credit_ledger.sql"));
    AssertTrue(File.Exists(migrationPath), "migration 012 should create the credit ledger.");
}

static void TestApiExposesPaywallEndpoints()
{
    var rootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var program = File.ReadAllText(Path.Combine(rootPath, "src", "OutfitPlanner.Api", "Program.cs"));
    var contracts = File.ReadAllText(Path.Combine(rootPath, "src", "OutfitPlanner.Api", "Contracts", "ApiContracts.cs"));

    AssertTrue(program.Contains("MapGet(\"/account/entitlements\"", StringComparison.Ordinal), "api should expose the account entitlements endpoint.");
    AssertTrue(program.Contains("MapPost(\"/admin/users/{userId}/credits\"", StringComparison.Ordinal), "api should expose the admin credit adjustment endpoint.");
    AssertTrue(program.Contains("LoadPlanCatalog", StringComparison.Ordinal), "api should build the plan catalog from configuration.");
    AssertTrue(contracts.Contains("AccountEntitlementsResponse", StringComparison.Ordinal), "contracts should document the entitlements response.");
    AssertTrue(contracts.Contains("RequiresUpgrade", StringComparison.Ordinal), "the try-on estimate should carry the paywall upgrade flag.");
}

static void TestApiExposesSecureAuthEndpoints()
{
    var programPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "OutfitPlanner.Api", "Program.cs"));
    var program = File.ReadAllText(programPath);

    AssertTrue(program.Contains("/auth/register", StringComparison.Ordinal), "api should expose email registration.");
    AssertTrue(program.Contains("/auth/login", StringComparison.Ordinal), "api should expose email login.");
    AssertTrue(program.Contains("/auth/logout", StringComparison.Ordinal), "api should expose logout.");
    AssertTrue(program.Contains("/auth/me", StringComparison.Ordinal), "api should expose current user session.");
    AssertTrue(program.Contains("/auth/external/{provider}/start", StringComparison.Ordinal), "api should expose OAuth/OIDC start endpoints.");
    AssertTrue(program.Contains("/auth/external/{provider}/complete", StringComparison.Ordinal), "api should complete external auth on a route separate from the provider callback path.");
    AssertTrue(program.Contains("/complete?returnUrl=", StringComparison.Ordinal), "external auth challenge should redirect to a completion route after the provider callback.");
    AssertTrue(program.Contains("UseForwardedHeaders", StringComparison.Ordinal), "api should honor forwarded proxy headers before auth.");
    AssertTrue(program.Contains("XForwardedProto", StringComparison.Ordinal), "api should preserve the browser-facing scheme for OAuth redirects.");
    AssertTrue(program.Contains("https://127.0.0.1:5173", StringComparison.Ordinal), "api CORS should allow HTTPS Vite dev origin.");
    AssertTrue(program.Contains("Authentication:PublicOrigin", StringComparison.Ordinal), "api should support a canonical public origin for stable external auth callbacks.");
    AssertTrue(program.Contains("CanonicalGoogleHandler", StringComparison.Ordinal), "google oauth should use the canonical public origin for token exchange callbacks.");
    AssertTrue(program.Contains("HttpOnly = true", StringComparison.Ordinal), "session cookies should be HttpOnly.");
    AssertTrue(program.Contains("SameSite = SameSiteMode.Lax", StringComparison.Ordinal), "auth cookies should use SameSite=Lax.");
    AssertTrue(program.Contains("X-CSRF-Token", StringComparison.Ordinal), "mutating authenticated requests should require a CSRF header.");
}

static void TestApiExposesPrivacyAndAuthHardeningEndpoints()
{
    var programPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "OutfitPlanner.Api", "Program.cs"));
    var program = File.ReadAllText(programPath);

    foreach (var route in new[]
    {
        "MapDelete(\"/account\"",
        "MapGet(\"/account/export\"",
        "MapDelete(\"/body-reference-photos/{photoId:guid}\"",
        "MapDelete(\"/try-on-jobs/{jobId:guid}/output\"",
        "MapPost(\"/privacy/purge-ai-outputs\"",
        "MapPost(\"/auth/email-verification/request\"",
        "MapPost(\"/auth/email-verification/confirm\"",
        "MapPost(\"/auth/password-reset/request\"",
        "MapPost(\"/auth/password-reset/confirm\"",
        "MapGet(\"/auth/sessions\"",
        "MapDelete(\"/auth/sessions\""
    })
    {
        AssertTrue(program.Contains(route, StringComparison.Ordinal), $"api should expose {route}.");
    }

    AssertTrue(program.Contains("AddRateLimiter", StringComparison.Ordinal), "api should configure rate limiting.");
    AssertTrue(program.Contains("login-rate-limit", StringComparison.Ordinal), "login route should use a rate limit policy.");
    AssertTrue(program.Contains("registration-rate-limit", StringComparison.Ordinal), "registration route should use a rate limit policy.");
}

static void TestApiExposesEditDeleteFilterAndRevokeEndpoints()
{
    var programPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "OutfitPlanner.Api", "Program.cs"));
    var program = File.ReadAllText(programPath);

    AssertTrue(program.Contains("MapGet(\"/garments/{garmentId:guid}\"", StringComparison.Ordinal), "api should expose garment detail reads.");
    AssertTrue(program.Contains("MapPatch(\"/garments/{garmentId:guid}\"", StringComparison.Ordinal), "api should expose garment edits.");
    AssertTrue(program.Contains("MapGet(\"/outfits/{outfitId:guid}\"", StringComparison.Ordinal), "api should expose outfit detail reads.");
    AssertTrue(program.Contains("MapPatch(\"/outfits/{outfitId:guid}\"", StringComparison.Ordinal), "api should expose outfit edits.");
    AssertTrue(program.Contains("MapDelete(\"/outfits/{outfitId:guid}\"", StringComparison.Ordinal), "api should expose outfit deletion.");
    AssertTrue(program.Contains("MapDelete(\"/outfits/{outfitId:guid}/try-on-preview\"", StringComparison.Ordinal), "api should expose active outfit preview deletion.");
    AssertTrue(program.Contains("MapPost(\"/outfits/{outfitId:guid}/try-on/estimate\"", StringComparison.Ordinal), "api should expose try-on estimate endpoint.");
    AssertTrue(program.Contains("MapDelete(\"/schedule/{date}\"", StringComparison.Ordinal), "api should expose unscheduling by date.");
    AssertTrue(program.Contains("MapDelete(\"/share/{token}\"", StringComparison.Ordinal), "api should expose share revocation.");
    AssertTrue(program.Contains("GarmentQuery", StringComparison.Ordinal), "garment list route should bind filter and pagination criteria.");
    AssertTrue(program.Contains("OutfitQuery", StringComparison.Ordinal), "outfit list route should bind filter and pagination criteria.");
}

static void TestApiExposesHairstylePresetEndpoints()
{
    var programPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "OutfitPlanner.Api", "Program.cs"));
    var program = File.ReadAllText(programPath);

    AssertTrue(program.Contains("MapGet(\"/hairstyles\"", StringComparison.Ordinal), "api should expose the hairstyle preset listing.");
    AssertTrue(program.Contains("MapGet(\"/hairstyles/assets/{fileName}\"", StringComparison.Ordinal), "api should serve hairstyle preset asset files.");
    AssertTrue(program.Contains("users.GetUserById(CurrentUser(context))?.Gender", StringComparison.Ordinal), "hairstyle listing should filter by the account gender.");
    AssertTrue(program.Contains("/hairstyles/assets/\", StringComparison.OrdinalIgnoreCase", StringComparison.Ordinal), "hairstyle asset GETs should be anonymous in the auth path rules.");
}

static void TestHairstylePresetCatalogServesVendoredManifest()
{
    var assetsPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "assets", "hairstyles"));
    var catalog = new ManifestHairstylePresetCatalog(assetsPath);

    var male = catalog.ListHairstylePresets(UserGender.Male);
    var female = catalog.ListHairstylePresets(UserGender.Female);
    AssertTrue(male.Count >= 8, $"the standard male set should have about ten presets (got {male.Count}).");
    AssertTrue(female.Count >= 8, $"the standard female set should have about ten presets (got {female.Count}).");
    AssertTrue(male.All(preset => preset.Gender == UserGender.Male), "the male listing must only contain male presets.");
    AssertTrue(female.All(preset => preset.Gender == UserGender.Female), "the female listing must only contain female presets.");
    AssertTrue(male.SequenceEqual(male.OrderBy(preset => preset.SortOrder).ToList()), "presets should come out ordered for display.");

    foreach (var preset in male.Concat(female))
    {
        AssertTrue(
            !preset.Id.Contains("afro", StringComparison.OrdinalIgnoreCase) && !preset.Name.Contains("afro", StringComparison.OrdinalIgnoreCase),
            $"preset {preset.Id} must not be an afro variant.");
        var asset = catalog.GetHairstyleAssetFile(preset.AssetFileName);
        AssertTrue(asset is not null, $"preset {preset.Id} must resolve its asset file.");
        AssertEqual("image/svg+xml", asset!.ContentType, $"preset {preset.Id} asset should be an SVG.");
        AssertTrue(File.Exists(asset.FullPath), $"preset {preset.Id} asset file must exist on disk.");
    }

    AssertTrue(catalog.GetHairstyleAssetFile("manifest.json") is null, "files outside the preset list must not be servable.");
    AssertTrue(catalog.GetHairstyleAssetFile("../../database/schema.sql") is null, "path traversal must not resolve assets.");
}

static void TestApiExposesOpenApiDocumentGeneration()
{
    var rootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var apiProject = File.ReadAllText(Path.Combine(rootPath, "src", "OutfitPlanner.Api", "OutfitPlanner.Api.csproj"));
    var program = File.ReadAllText(Path.Combine(rootPath, "src", "OutfitPlanner.Api", "Program.cs"));

    AssertTrue(apiProject.Contains("Microsoft.AspNetCore.OpenApi", StringComparison.Ordinal), "api project should reference ASP.NET Core OpenAPI runtime package.");
    AssertTrue(apiProject.Contains("Microsoft.Extensions.ApiDescription.Server", StringComparison.Ordinal), "api project should reference build-time OpenAPI generation package.");
    AssertTrue(program.Contains("builder.Services.AddOpenApi()", StringComparison.Ordinal), "api startup should register OpenAPI services.");
    AssertTrue(program.Contains("app.MapOpenApi(\"/api/openapi/{documentName}.json\")", StringComparison.Ordinal), "api startup should map OpenAPI JSON under /api.");
    AssertTrue(program.Contains("path.StartsWith(\"/openapi/\", StringComparison.OrdinalIgnoreCase)", StringComparison.Ordinal), "OpenAPI endpoint should not require an auth session.");
    AssertTrue(program.Contains("IsOpenApiDocumentGeneration()", StringComparison.Ordinal), "api startup should detect build-time OpenAPI document generation.");
    AssertTrue(program.Contains("if (!IsOpenApiDocumentGeneration())", StringComparison.Ordinal), "api startup should skip migration side effects during OpenAPI document generation.");
    AssertTrue(program.Contains("Environment.CommandLine", StringComparison.Ordinal), "OpenAPI generation detection should inspect the process command line.");
    AssertTrue(program.Contains("dotnet-getdocument", StringComparison.Ordinal), "OpenAPI generation detection should recognize the getdocument host tool.");
    AssertTrue(program.Contains("GetDocument.Insider", StringComparison.Ordinal), "OpenAPI generation detection should recognize the inner getdocument tool.");
    AssertTrue(!program.Contains("Assembly.GetEntryAssembly()?.GetName().Name", StringComparison.Ordinal), "OpenAPI generation detection should not rely on the entry assembly name.");
}

static void TestApiDocumentsFrontendResponseBodies()
{
    var rootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var program = File.ReadAllText(Path.Combine(rootPath, "src", "OutfitPlanner.Api", "Program.cs"));
    var contracts = File.ReadAllText(Path.Combine(rootPath, "src", "OutfitPlanner.Api", "Contracts", "ApiContracts.cs"));

    foreach (var requiredMetadata in new[]
    {
        ".Produces<IReadOnlyList<BodyReferencePhoto>>(StatusCodes.Status200OK)",
        ".Produces<BodyReferencePhoto>(StatusCodes.Status201Created)",
        ".Produces<IReadOnlyList<GarmentItem>>(StatusCodes.Status200OK)",
        ".Produces<GarmentItem>(StatusCodes.Status200OK)",
        ".Produces<GarmentItem>(StatusCodes.Status201Created)",
        ".Produces<IReadOnlyList<Outfit>>(StatusCodes.Status200OK)",
        ".Produces<Outfit>(StatusCodes.Status200OK)",
        ".Produces<Outfit>(StatusCodes.Status201Created)",
        ".Produces<TryOnEstimateResponse>(StatusCodes.Status200OK)",
        ".Produces<TryOnJob>(StatusCodes.Status202Accepted)",
        ".Produces<IReadOnlyList<ScheduledOutfit>>(StatusCodes.Status200OK)",
        ".Produces<ScheduledOutfit>(StatusCodes.Status200OK)",
        ".Produces<ShareLinkResponse>(StatusCodes.Status200OK)",
        ".Produces<SharedOutfitResponse>(StatusCodes.Status200OK)"
    })
    {
        AssertTrue(program.Contains(requiredMetadata, StringComparison.Ordinal), $"api should document response metadata {requiredMetadata}.");
    }

    AssertTrue(program.Contains(".Produces(StatusCodes.Status404NotFound)", StringComparison.Ordinal), "detail routes should document 404 responses.");
    AssertTrue(contracts.Contains("public sealed record SharedOutfitResponse", StringComparison.Ordinal), "shared outfit response should be a named API contract.");
    AssertTrue(contracts.Contains("public sealed record EstimateTryOnRequest", StringComparison.Ordinal), "estimate try-on request should be a named API contract.");
    AssertTrue(contracts.Contains("public sealed record TryOnEstimateResponse", StringComparison.Ordinal), "try-on estimate response should be a named API contract.");
    AssertTrue(contracts.Contains("public sealed record TryOnEstimateItemResponse", StringComparison.Ordinal), "try-on estimate items should be named API contracts.");
    AssertTrue(contracts.Contains("TryOnMode TryOnMode", StringComparison.Ordinal), "start request should include try-on mode.");
    AssertTrue(contracts.Contains("int ConfirmedCredits", StringComparison.Ordinal), "start request should include confirmed credits.");
    AssertTrue(contracts.Contains("string ConfirmedCacheKey", StringComparison.Ordinal), "start request should include confirmed cache key.");
    AssertTrue(contracts.Contains("string? OriginalUrl", StringComparison.Ordinal), "uploaded photo response should expose original URL.");
    AssertTrue(contracts.Contains("string? ThumbnailUrl", StringComparison.Ordinal), "uploaded photo response should expose thumbnail URL.");
    AssertTrue(contracts.Contains("string? CutoutUrl", StringComparison.Ordinal), "uploaded photo response should expose garment cutout URL.");
}

static void TestWardrobeServiceUpdatesStructuredMetadata()
{
    var store = new InMemoryOutfitStore();
    var service = new WardrobeService(store, store, new SystemClock());
    var garment = service.CreateGarment(CreateGarment("user-a", "linen shirt", GarmentCategory.Top));
    var lastWorn = new DateTimeOffset(2026, 6, 1, 9, 30, 0, TimeSpan.Zero);

    var updated = service.UpdateGarment("user-a", garment.Id, new UpdateGarmentCommand(
        Name: "black linen trousers",
        Category: GarmentCategory.Bottom,
        Tags: new[] { "linen", "capsule" },
        PrimaryColor: "black",
        SecondaryColors: new[] { "charcoal", "white" },
        Material: "linen",
        Brand: "Muji",
        Size: "M",
        Season: new[] { "summer" },
        WeatherMinTemp: 18,
        WeatherMaxTemp: 30,
        Occasion: new[] { "casual", "date" },
        FormalityScore: 2,
        WarmthScore: 1,
        ComfortScore: 5,
        IsFavorite: true,
        IsArchived: false,
        LastWornAt: lastWorn,
        LaundryStatus: "worn"));

    AssertTrue(updated is not null, "existing garment should update.");
    AssertEqual("black linen trousers", updated!.Name, "garment name should be editable.");
    AssertEqual(GarmentCategory.Bottom, updated.Category, "garment category should be editable.");
    AssertEqual(BodyZone.Legs, updated.BodyZone, "body zone should follow the edited category.");
    AssertEqual("black", updated.PrimaryColor, "primary color should be structured metadata.");
    AssertEqual(2, updated.SecondaryColors.Count, "secondary colors should be structured metadata.");
    AssertEqual("linen", updated.Material, "material should be structured metadata.");
    AssertEqual("Muji", updated.Brand, "brand should be structured metadata.");
    AssertEqual("M", updated.Size, "size should be structured metadata.");
    AssertEqual(1, updated.Season.Count, "season should be structured metadata.");
    AssertEqual(18, updated.WeatherMinTemp, "weather min temp should update.");
    AssertEqual(30, updated.WeatherMaxTemp, "weather max temp should update.");
    AssertEqual(2, updated.Occasion.Count, "occasion should be structured metadata.");
    AssertEqual(2, updated.FormalityScore, "formality score should update.");
    AssertEqual(1, updated.WarmthScore, "warmth score should update.");
    AssertEqual(5, updated.ComfortScore, "comfort score should update.");
    AssertTrue(updated.IsFavorite, "favorite flag should update.");
    AssertTrue(!updated.IsArchived, "archive flag should update.");
    AssertEqual(lastWorn, updated.LastWornAt, "last worn timestamp should update.");
    AssertEqual("worn", updated.LaundryStatus, "laundry status should update.");
    AssertEqual(2, updated.Tags.Count, "free-form tags should remain available.");
    AssertTrue(service.UpdateGarment("user-b", garment.Id, new UpdateGarmentCommand(Name: "stolen")) is null, "other users must not update the garment.");
}

static void TestWardrobeServiceAutoStraightensClothingOnly()
{
    var store = new InMemoryOutfitStore();
    var rotator = new RecordingGarmentImageRotator { AutoStraightenAngle = 12d };
    var service = new WardrobeService(store, store, new SystemClock(), null, rotator);

    var top = service.CreateGarment(CreateGarment("user-a", "linen shirt", GarmentCategory.Top));
    AssertEqual(1, rotator.RotateCalls, "clothing categories should be auto-straightened on create");
    AssertTrue(Math.Abs(top.RotationDegrees - 12d) < 0.001, "auto-straighten angle should be persisted on the garment");
    AssertTrue(top.ImageUrl.Contains("#cutout", StringComparison.Ordinal), "auto-straighten should swap in the rotated cutout url");

    var rotateCallsBeforeBag = rotator.RotateCalls;
    var bag = service.CreateGarment(CreateGarment("user-a", "tote bag", GarmentCategory.Bag));
    AssertEqual(rotateCallsBeforeBag, rotator.RotateCalls, "non-clothing categories should not be auto-straightened");
    AssertEqual(0d, bag.RotationDegrees, "non-clothing garment should keep zero rotation");
}

static void TestWardrobeServiceRotatesGarmentOnUpdate()
{
    var store = new InMemoryOutfitStore();
    var rotator = new RecordingGarmentImageRotator { AutoStraightenAngle = 0d };
    var service = new WardrobeService(store, store, new SystemClock(), null, rotator);
    var garment = service.CreateGarment(CreateGarment("user-a", "tote bag", GarmentCategory.Bag));

    var rotated = service.UpdateGarment("user-a", garment.Id, new UpdateGarmentCommand(RotationDegrees: 90d));
    AssertTrue(rotated is not null, "garment should update");
    AssertTrue(Math.Abs(rotated!.RotationDegrees - 90d) < 0.001, "manual rotation angle should be persisted");
    AssertTrue(rotated.ImageUrl.Contains("#cutout", StringComparison.Ordinal), "manual rotation should re-render and swap the cutout url");
    AssertEqual(90d, rotator.LastRotateDegrees, "rotator should be asked for the requested absolute angle");

    var metadataOnly = service.UpdateGarment("user-a", garment.Id, new UpdateGarmentCommand(Name: "weekender bag"));
    AssertTrue(Math.Abs(metadataOnly!.RotationDegrees - 90d) < 0.001, "a metadata-only edit should preserve the saved rotation");
}

static void TestWardrobeServicePersistsAndRefreshesCutoutMeasurement()
{
    var store = new InMemoryOutfitStore();
    var rotator = new RecordingGarmentImageRotator
    {
        AutoStraightenAngle = 0d,
        RotationMeasurement = new GarmentCutoutMeasurement(300, 100)
    };
    var service = new WardrobeService(store, store, new SystemClock(), null, rotator);

    var garment = service.CreateGarment(CreateGarment("user-a", "tote bag", GarmentCategory.Bag) with
    {
        CutoutWidthPx = 100,
        CutoutHeightPx = 300
    });
    AssertEqual(100, garment.CutoutWidthPx, "upload-time cutout width should persist on the garment");
    AssertEqual(300, garment.CutoutHeightPx, "upload-time cutout height should persist on the garment");

    var rotated = service.UpdateGarment("user-a", garment.Id, new UpdateGarmentCommand(RotationDegrees: 90d));
    AssertEqual(300, rotated!.CutoutWidthPx, "manual rotation should refresh the measurement from the re-rendered cutout");
    AssertEqual(100, rotated.CutoutHeightPx, "manual rotation should refresh the measurement from the re-rendered cutout");

    var metadataOnly = service.UpdateGarment("user-a", garment.Id, new UpdateGarmentCommand(Name: "weekender"));
    AssertEqual(300, metadataOnly!.CutoutWidthPx, "a metadata-only edit should preserve the measurement");

    // Garbage measurements (a lone dimension, zero, negatives) degrade to "not measured".
    var unmeasured = service.CreateGarment(CreateGarment("user-a", "belt", GarmentCategory.Accessory) with
    {
        CutoutWidthPx = -5,
        CutoutHeightPx = 10
    });
    AssertTrue(unmeasured.CutoutWidthPx is null && unmeasured.CutoutHeightPx is null, "invalid measurements should be dropped");

    // The backfill worker's repository surface: unmeasured garments with a finished cutout are
    // selected, and the column-scoped update fills them without rewriting the record.
    var missing = store.ListGarmentsMissingCutoutMeasurement(10);
    AssertTrue(missing.Any(item => item.Id == unmeasured.Id), "unmeasured garments should be selected for backfill");
    AssertTrue(missing.All(item => item.Id != garment.Id), "measured garments should not be selected for backfill");

    store.UpdateGarmentCutoutMeasurement(unmeasured.Id, 42, 84);
    var backfilled = store.GetGarmentByUser("user-a", unmeasured.Id);
    AssertEqual(42, backfilled!.CutoutWidthPx, "the column-scoped update should persist the measurement");
    AssertEqual(84, backfilled.CutoutHeightPx, "the column-scoped update should persist the measurement");
    AssertEqual("belt", backfilled.Name, "the column-scoped update should leave the rest of the record intact");
    AssertEqual(0, store.ListGarmentsMissingCutoutMeasurement(10).Count, "backfilled garments should stop being selected");
}

static void TestWardrobeServiceFiltersSortsAndPaginatesGarments()
{
    var store = new InMemoryOutfitStore();
    var service = new WardrobeService(store, store, new SystemClock());
    var summerShirt = service.CreateGarment(new CreateGarmentCommand(
        "user-a",
        "black summer shirt",
        GarmentCategory.Top,
        "https://example.com/black-shirt.jpg",
        null,
        new[] { "shirt", "linen" },
        PrimaryColor: "black",
        Season: new[] { "summer" },
        Occasion: new[] { "casual" },
        IsFavorite: true));
    service.CreateGarment(new CreateGarmentCommand(
        "user-a",
        "white winter shirt",
        GarmentCategory.Top,
        "https://example.com/white-shirt.jpg",
        null,
        new[] { "shirt" },
        PrimaryColor: "white",
        Season: new[] { "winter" }));
    service.CreateGarment(new CreateGarmentCommand(
        "user-a",
        "black jeans",
        GarmentCategory.Bottom,
        "https://example.com/black-jeans.jpg",
        null,
        new[] { "denim" },
        PrimaryColor: "black",
        Season: new[] { "summer" }));

    var result = service.ListGarments("user-a", new GarmentQuery(
        Category: GarmentCategory.Top,
        Color: "black",
        Season: "summer",
        Search: "shirt",
        Sort: "recent",
        Offset: 0,
        Limit: 1,
        Favorite: true));

    AssertEqual(1, result.Count, "filters and limit should narrow the result set.");
    AssertEqual(summerShirt.Id, result[0].Id, "filtered result should match category color season q and favorite.");
}

static void TestOutfitServiceUpdatesFiltersAndDeletesOutfits()
{
    var store = new InMemoryOutfitStore();
    var service = new OutfitService(store, store, new SystemClock());
    var top = store.CreateGarment(CreateGarment("user-a", "oxford shirt", GarmentCategory.Top));
    var bottom = store.CreateGarment(CreateGarment("user-a", "tailored trousers", GarmentCategory.Bottom));
    var outfit = service.CreateOutfit("user-a", "work fit", new[] { top.Id });

    var updated = service.UpdateOutfit("user-a", outfit.Id, new UpdateOutfitCommand(
        Name: "office uniform",
        GarmentIds: new[] { top.Id, bottom.Id },
        Tags: new[] { "office" },
        Occasion: new[] { "business" },
        IsFavorite: true,
        IsArchived: false));
    var detail = service.GetOutfit("user-a", outfit.Id);
    var filtered = service.ListOutfits("user-a", new OutfitQuery(Search: "office", Occasion: "business", Favorite: true));

    AssertTrue(updated is not null, "existing outfit should update.");
    AssertEqual("office uniform", updated!.Name, "outfit name should update.");
    AssertEqual(2, updated.Items.Count, "outfit garments should update.");
    AssertEqual(1, updated.Tags.Count, "outfit tags should update.");
    AssertEqual(1, updated.Occasion.Count, "outfit occasion should update.");
    AssertTrue(updated.IsFavorite, "outfit favorite flag should update.");
    AssertTrue(detail is not null, "outfit detail should be readable.");
    AssertEqual(1, filtered.Count, "outfit filters should find the updated outfit.");
    AssertTrue(service.DeleteOutfit("user-a", outfit.Id), "owner should delete outfit.");
    AssertTrue(service.GetOutfit("user-a", outfit.Id) is null, "deleted outfit detail should disappear.");
    AssertTrue(!service.DeleteOutfit("user-b", outfit.Id), "other users must not delete outfit.");
}

static void TestOutfitSlotCompatibilityRules()
{
    var store = new InMemoryOutfitStore();
    var service = new OutfitService(store, store, new SystemClock());
    var top = store.CreateGarment(CreateGarment("user-a", "white tee", GarmentCategory.Top));
    var bottom = store.CreateGarment(CreateGarment("user-a", "black trousers", GarmentCategory.Bottom));
    var secondBottom = store.CreateGarment(CreateGarment("user-a", "blue jeans", GarmentCategory.Bottom));
    var dress = store.CreateGarment(CreateGarment("user-a", "black dress", GarmentCategory.Dress));
    var outerwear = store.CreateGarment(CreateGarment("user-a", "wool coat", GarmentCategory.Outerwear));
    var shoes = store.CreateGarment(CreateGarment("user-a", "leather shoes", GarmentCategory.Shoes));
    var secondShoes = store.CreateGarment(CreateGarment("user-a", "white sneakers", GarmentCategory.Shoes));
    var bag = store.CreateGarment(CreateGarment("user-a", "crossbody bag", GarmentCategory.Bag));

    AssertEqual(3, service.CreateOutfit("user-a", "top bottom shoes", new[] { top.Id, bottom.Id, shoes.Id }).Items.Count, "top bottom shoes should be compatible.");
    AssertEqual(2, service.CreateOutfit("user-a", "dress shoes", new[] { dress.Id, shoes.Id }).Items.Count, "dress shoes should be compatible.");
    AssertEqual(4, service.CreateOutfit("user-a", "layers", new[] { top.Id, bottom.Id, outerwear.Id, shoes.Id }).Items.Count, "top bottom outerwear shoes should be compatible.");
    AssertEqual(3, service.CreateOutfit("user-a", "dress coat shoes", new[] { dress.Id, outerwear.Id, shoes.Id }).Items.Count, "dress outerwear shoes should be compatible.");
    AssertEqual(4, service.CreateOutfit("user-a", "accessorized", new[] { top.Id, bottom.Id, shoes.Id, bag.Id }).Items.Count, "bag should not conflict with base slots.");

    AssertThrows<InvalidOperationException>(
        () => service.CreateOutfit("user-a", "dress with tee", new[] { dress.Id, top.Id, shoes.Id }),
        "full body garments must conflict with torso garments");
    AssertThrows<InvalidOperationException>(
        () => service.CreateOutfit("user-a", "double bottoms", new[] { top.Id, bottom.Id, secondBottom.Id, shoes.Id }),
        "two bottom garments should require explicit layering mode");
    AssertThrows<InvalidOperationException>(
        () => service.CreateOutfit("user-a", "double shoes", new[] { top.Id, bottom.Id, shoes.Id, secondShoes.Id }),
        "two shoe garments should be rejected");
}

static void TestOutfitRulesPreservesGarmentRotation()
{
    var store = new InMemoryOutfitStore();
    var garment = store.CreateGarment(CreateGarment("user-a", "tilted tee", GarmentCategory.Top)) with { RotationDegrees = 15d };

    var items = OutfitRules.BuildItems(new[] { garment });

    AssertEqual(1, items.Count, "a single garment should yield one outfit item.");
    AssertTrue(Math.Abs(items[0].RotationDegrees - 15d) < 0.001, "BuildItems should carry the garment rotation onto the outfit item so rotation reaches responses, persistence, and the try-on cache key.");
}

static void TestScheduleServiceUnschedulesDate()
{
    var store = new InMemoryOutfitStore();
    var outfitService = new OutfitService(store, store, new SystemClock());
    var scheduleService = new ScheduleService(store, store, new SystemClock());
    var day = new DateOnly(2026, 6, 8);
    var top = store.CreateGarment(CreateGarment("user-a", "white tee", GarmentCategory.Top));
    var outfit = outfitService.CreateOutfit("user-a", "casual", new[] { top.Id });
    scheduleService.ScheduleOutfit("user-a", day, outfit.Id);

    AssertTrue(scheduleService.UnscheduleOutfit("user-a", day), "owner should unschedule a planned date.");
    AssertEqual(0, scheduleService.GetSchedule("user-a", day, day).Count, "unscheduled date should disappear from schedule.");
    AssertTrue(!scheduleService.UnscheduleOutfit("user-b", day), "other users must not unschedule the date.");
}

static void TestShareServiceRevokesShareLinks()
{
    var store = new InMemoryOutfitStore();
    var outfitService = new OutfitService(store, store, new SystemClock());
    var share = new ShareService(store, store, new TestShareTokenGenerator(), new SystemClock());
    var top = store.CreateGarment(CreateGarment("user-a", "white tee", GarmentCategory.Top));
    var outfit = outfitService.CreateOutfit("user-a", "casual", new[] { top.Id });
    var link = share.CreateShareLink("user-a", outfit.Id);

    AssertTrue(share.GetSharedOutfit(link.Token) is not null, "fresh share link should resolve.");
    AssertTrue(!share.RevokeShareLink("user-b", link.Token), "other users must not revoke the share link.");
    AssertTrue(share.RevokeShareLink("user-a", link.Token), "owner should revoke the share link.");
    AssertTrue(share.GetSharedOutfit(link.Token) is null, "revoked share link should no longer resolve.");
}

static void TestTryOnCostEstimatorClassifiesAndPricesModes()
{
    var outfit = CreateOutfitWithItems(
        new OutfitItem(Guid.Parse("10000000-0000-0000-0000-000000000001"), "white tee", GarmentCategory.Top, BodyZone.Torso, "https://app.test/top.png"),
        new OutfitItem(Guid.Parse("10000000-0000-0000-0000-000000000002"), "jeans", GarmentCategory.Bottom, BodyZone.Legs, "https://app.test/bottom.png"),
        new OutfitItem(Guid.Parse("10000000-0000-0000-0000-000000000003"), "silk dress", GarmentCategory.Dress, BodyZone.FullBody, "https://app.test/dress.png"),
        new OutfitItem(Guid.Parse("10000000-0000-0000-0000-000000000004"), "trench coat", GarmentCategory.Outerwear, BodyZone.OuterLayer, "https://app.test/outerwear.png"),
        new OutfitItem(Guid.Parse("10000000-0000-0000-0000-000000000005"), "loafers", GarmentCategory.Shoes, BodyZone.Feet, "https://app.test/shoes.png"),
        new OutfitItem(Guid.Parse("10000000-0000-0000-0000-000000000006"), "bag", GarmentCategory.Bag, BodyZone.Accessory, "https://app.test/bag.png"),
        new OutfitItem(Guid.Parse("10000000-0000-0000-0000-000000000007"), "scarf", GarmentCategory.Accessory, BodyZone.Accessory, "https://app.test/scarf.png"));
    var estimator = new TryOnCostEstimator();

    var sequential = estimator.Estimate(outfit, new TryOnEstimateInput(
        TryOnMode.SequentialOutfitTryOn,
        "FashnTryOnProvider",
        "body:body-1",
        "settings-a",
        hasCachedResult: false));
    var composite = estimator.Estimate(outfit, new TryOnEstimateInput(
        TryOnMode.ExperimentalCompositeTryOn,
        "CompositeFashnTryOnProvider",
        "body:body-1",
        "settings-a",
        hasCachedResult: true));

    AssertEqual(4, sequential.BodyTryOnItems.Count, "sequential estimate should classify body try-on items.");
    AssertTrue(sequential.BodyTryOnItems.Any(item => item.Category == GarmentCategory.Top), "top should be a body try-on category.");
    AssertTrue(sequential.BodyTryOnItems.Any(item => item.Category == GarmentCategory.Bottom), "bottom should be a body try-on category.");
    AssertTrue(sequential.BodyTryOnItems.Any(item => item.Category == GarmentCategory.Dress), "dress should be a body try-on category.");
    AssertTrue(sequential.BodyTryOnItems.Any(item => item.Category == GarmentCategory.Outerwear), "outerwear should be a body try-on category.");
    AssertEqual(3, sequential.VisualOnlyItems.Count, "sequential estimate should classify visual-only items.");
    AssertTrue(sequential.VisualOnlyItems.Any(item => item.Category == GarmentCategory.Shoes), "shoes should be a visual-only category.");
    AssertTrue(sequential.VisualOnlyItems.Any(item => item.Category == GarmentCategory.Bag), "bag should be a visual-only category.");
    AssertTrue(sequential.VisualOnlyItems.Any(item => item.Category == GarmentCategory.Accessory), "accessory should be a visual-only category.");
    AssertEqual(4, sequential.EstimatedCredits, "sequential estimate should cost one credit per body try-on item.");
    AssertTrue(sequential.IsAvailable, "sequential estimate should be available for multiple body items.");
    AssertTrue(sequential.RequiresAi, "sequential estimate should require AI.");
    AssertTrue(!sequential.RequiresPremiumConfirmation, "sequential estimate should not be premium.");
    AssertEqual(4, sequential.IncludedGarmentIds.Count, "sequential estimate should include only body try-on items.");
    AssertEqual(3, sequential.ExcludedGarmentIds.Count, "sequential estimate should exclude visual-only items.");
    AssertTrue(sequential.CacheKey.Length == 64, "cache key should be a SHA-256 hex string.");
    AssertTrue(sequential.CacheKey.All(character => char.IsDigit(character) || character is >= 'a' and <= 'f'), "cache key should be lowercase SHA-256 hex.");

    AssertEqual(1, composite.EstimatedCredits, "composite estimate should cost one credit.");
    AssertEqual(7, composite.IncludedGarmentIds.Count, "composite estimate should include body and visual items.");
    AssertTrue(composite.RequiresPremiumConfirmation, "composite estimate should require premium confirmation.");
    AssertTrue(composite.HasCachedResult, "estimate should carry cache hit status from the caller.");
}

static void TestTryOnCacheKeyVariesWithGarmentRotation()
{
    var estimator = new TryOnCostEstimator();
    var garmentId = Guid.NewGuid();

    Outfit OutfitWithRotation(double degrees) => new Outfit(
        Guid.NewGuid(),
        "user-a",
        "tee outfit",
        new[] { new OutfitItem(garmentId, "tee", GarmentCategory.Top, BodyZone.Torso, "https://app.test/tee.png", degrees) },
        Array.Empty<string>(),
        Array.Empty<string>(),
        false,
        false,
        null,
        null,
        DateTimeOffset.UtcNow);

    var input = new TryOnEstimateInput(TryOnMode.SingleGarmentTryOn, "FashnTryOnProvider", "body:body-1", "settings-a", hasCachedResult: false);

    var upright = estimator.Estimate(OutfitWithRotation(0d), input).CacheKey;
    var tilted = estimator.Estimate(OutfitWithRotation(90d), input).CacheKey;
    var uprightAgain = estimator.Estimate(OutfitWithRotation(0d), input).CacheKey;

    AssertTrue(upright != tilted, "rotating a garment should change the try-on cache key so stale results are not reused");
    AssertEqual(upright, uprightAgain, "the same rotation should produce the same try-on cache key");
}

static void TestTryOnCostEstimatorMarksUnavailableModes()
{
    var outfit = CreateOutfitWithItems(
        new OutfitItem(Guid.NewGuid(), "white tee", GarmentCategory.Top, BodyZone.Torso, "https://app.test/top.png"),
        new OutfitItem(Guid.NewGuid(), "jeans", GarmentCategory.Bottom, BodyZone.Legs, "https://app.test/bottom.png"),
        new OutfitItem(Guid.NewGuid(), "bag", GarmentCategory.Bag, BodyZone.Accessory, "https://app.test/bag.png"));
    var visualOnlyOutfit = CreateOutfitWithItems(
        new OutfitItem(Guid.NewGuid(), "bag", GarmentCategory.Bag, BodyZone.Accessory, "https://app.test/bag.png"));
    var estimator = new TryOnCostEstimator();

    var single = estimator.Estimate(outfit, new TryOnEstimateInput(
        TryOnMode.SingleGarmentTryOn,
        "FashnTryOnProvider",
        "body:body-1",
        "settings-a",
        hasCachedResult: false));
    var visualOnlySingle = estimator.Estimate(visualOnlyOutfit, new TryOnEstimateInput(
        TryOnMode.SingleGarmentTryOn,
        "FashnTryOnProvider",
        "body:body-1",
        "settings-a",
        hasCachedResult: false));
    var visualOnly = estimator.Estimate(visualOnlyOutfit, new TryOnEstimateInput(
        TryOnMode.SequentialOutfitTryOn,
        "FashnTryOnProvider",
        "body:body-1",
        "settings-a",
        hasCachedResult: false));
    var clothesOnly = estimator.Estimate(visualOnlyOutfit, new TryOnEstimateInput(
        TryOnMode.ClothesOnlyPreview,
        "MockTryOnProvider",
        "body:body-1",
        "settings-a",
        hasCachedResult: false));

    AssertTrue(!single.IsAvailable, "single mode should reject multiple body try-on items.");
    AssertTrue(single.Summary.Contains("one body garment", StringComparison.OrdinalIgnoreCase), "single mode should explain the shape issue.");
    AssertEqual(0, single.IncludedGarmentIds.Count, "invalid single mode should not include multiple body try-on items.");
    AssertEqual(
        TryOnCostEstimator.BuildCacheKey("body:body-1", Array.Empty<Guid>(), "FashnTryOnProvider", TryOnMode.SingleGarmentTryOn, "settings-a"),
        single.CacheKey,
        "invalid single mode cache key should not be based on multiple garments.");
    AssertTrue(!visualOnlySingle.IsAvailable, "single paid mode should reject visual-only outfits.");
    AssertEqual(0, visualOnlySingle.IncludedGarmentIds.Count, "visual-only single mode should not include garments.");
    AssertTrue(visualOnlySingle.Warnings.Any(warning => warning.Contains("ClothesOnlyPreview", StringComparison.Ordinal)), "visual-only single estimate should recommend clothes-only mode.");
    AssertTrue(!visualOnly.IsAvailable, "paid normal modes should reject visual-only outfits.");
    AssertTrue(visualOnly.Warnings.Any(warning => warning.Contains("ClothesOnlyPreview", StringComparison.Ordinal)), "visual-only estimate should recommend clothes-only mode.");
    AssertTrue(clothesOnly.IsAvailable, "clothes-only mode should be available for visual-only outfits.");
    AssertEqual(0, clothesOnly.EstimatedCredits, "clothes-only mode should be free.");
    AssertTrue(!clothesOnly.RequiresAi, "clothes-only mode should not require AI.");
}

static void TestTryOnConsentRequired()
{
    var store = new InMemoryOutfitStore();
    var userId = "user-a";
    var top = store.CreateGarment(CreateGarment(userId, "white tee", GarmentCategory.Top));
    var bottom = store.CreateGarment(CreateGarment(userId, "jeans", GarmentCategory.Bottom));
    var outfit = new OutfitService(store, store, new SystemClock())
        .CreateOutfit(userId, "casual", new[] { top.Id });
    var provider = new CountingTryOnProvider();
    var service = new TryOnService(store, store, store, new RecordingTryOnJobQueue(), provider, new TryOnCostEstimator(), new SystemClock());
    var estimate = service.Estimate(userId, outfit.Id, TryOnMode.SingleGarmentTryOn, "https://example.com/person.jpg", null);

    AssertThrows<InvalidOperationException>(
        () => service.StartAsync(userId, outfit.Id, "https://example.com/person.jpg", consentAccepted: false, TryOnMode.SingleGarmentTryOn, estimate.EstimatedCredits, estimate.CacheKey).GetAwaiter().GetResult(),
        "try-on should require consent");
    AssertEqual(0, provider.Calls, "provider must not receive photos without consent");
}

static void TestTryOnServiceBlocksAiUntilGenderIsSet()
{
    var store = new InMemoryOutfitStore();
    var userId = "user-a";
    EnsureTestUser(store, userId, gender: null);
    var top = store.CreateGarment(CreateGarment(userId, "white tee", GarmentCategory.Top));
    var outfit = new OutfitService(store, store, new SystemClock())
        .CreateOutfit(userId, "casual", new[] { top.Id });
    var provider = new CountingTryOnProvider();
    var service = new TryOnService(store, store, store, new RecordingTryOnJobQueue(), provider, new TryOnCostEstimator(), new SystemClock());

    var aiEstimate = service.Estimate(userId, outfit.Id, TryOnMode.SingleGarmentTryOn, "https://example.com/person.jpg", null);
    var freeEstimate = service.Estimate(userId, outfit.Id, TryOnMode.ClothesOnlyPreview, "", null);

    AssertTrue(!aiEstimate.IsAvailable, "AI try-on should be unavailable until gender is set.");
    AssertTrue(aiEstimate.Warnings.Any(warning => warning.Contains("gender", StringComparison.OrdinalIgnoreCase)), "unavailable estimate should explain the missing gender.");
    AssertTrue(freeEstimate.IsAvailable, "clothes-only preview should remain available without gender.");
    AssertThrows<InvalidOperationException>(
        () => service.StartAsync(userId, outfit.Id, "https://example.com/person.jpg", consentAccepted: true, TryOnMode.SingleGarmentTryOn, aiEstimate.EstimatedCredits, aiEstimate.CacheKey).GetAwaiter().GetResult(),
        "AI generation should not start until gender is set.");

    store.UpdateUser(store.GetUserById(userId)! with { Gender = UserGender.Female });
    var availableEstimate = service.Estimate(userId, outfit.Id, TryOnMode.SingleGarmentTryOn, "https://example.com/person.jpg", null);

    AssertTrue(availableEstimate.IsAvailable, "AI try-on should become available after gender is set.");
}

static void TestTryOnServiceEstimatesCost()
{
    var store = new InMemoryOutfitStore();
    var userId = "user-a";
    var top = store.CreateGarment(CreateGarment(userId, "white tee", GarmentCategory.Top));
    var bottom = store.CreateGarment(CreateGarment(userId, "jeans", GarmentCategory.Bottom));
    var bag = store.CreateGarment(CreateGarment(userId, "bag", GarmentCategory.Bag));
    var outfit = new OutfitService(store, store, new SystemClock())
        .CreateOutfit(userId, "casual", new[] { top.Id, bottom.Id, bag.Id });
    var service = new TryOnService(store, store, store, new RecordingTryOnJobQueue(), new CountingTryOnProvider(), new TryOnCostEstimator(), new SystemClock());

    var estimate = service.Estimate(userId, outfit.Id, TryOnMode.SequentialOutfitTryOn, "https://example.com/person.jpg", null);

    AssertEqual(2, estimate.EstimatedCredits, "estimate should cost one credit per body garment.");
    AssertEqual(2, estimate.BodyTryOnItems.Count, "estimate should classify body try-on items.");
    AssertEqual(1, estimate.VisualOnlyItems.Count, "estimate should classify visual-only items.");
    AssertTrue(estimate.Warnings.Any(warning => warning.Contains("visual-only", StringComparison.OrdinalIgnoreCase)), "estimate should warn about excluded visual-only items.");
}

static void TestTryOnServiceMarksProviderUnsupportedModesUnavailable()
{
    var store = new InMemoryOutfitStore();
    var userId = "user-a";
    var top = store.CreateGarment(CreateGarment(userId, "white tee", GarmentCategory.Top));
    var outfit = new OutfitService(store, store, new SystemClock())
        .CreateOutfit(userId, "casual", new[] { top.Id });
    var provider = new CountingTryOnProvider(TryOnMode.SingleGarmentTryOn, TryOnMode.SequentialOutfitTryOn);
    var service = new TryOnService(store, store, store, new RecordingTryOnJobQueue(), provider, new TryOnCostEstimator(), new SystemClock());

    var estimate = service.Estimate(userId, outfit.Id, TryOnMode.ExperimentalCompositeTryOn, "https://example.com/person.jpg", null);

    AssertTrue(!estimate.IsAvailable, "estimate should mark unsupported provider modes unavailable.");
    AssertTrue(estimate.Summary.Contains("does not support", StringComparison.OrdinalIgnoreCase), "estimate should explain provider capability mismatch.");
}

static void TestTryOnServiceExposesOnlyConfirmedStartContract()
{
    var servicePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "OutfitPlanner.Application", "Services", "TryOnService.cs"));
    var source = File.ReadAllText(servicePath);

    AssertTrue(!source.Contains("sequentialFlowEnabled = false", StringComparison.Ordinal), "try-on service should not expose a legacy auto-confirming sequential flow start overload.");
    AssertTrue(!source.Contains("public TryOnJob Start(", StringComparison.Ordinal), "try-on service should not expose a sync auto-confirming start overload.");
}

static void TestTryOnServiceEnforcesConfirmedCost()
{
    var store = new InMemoryOutfitStore();
    var userId = "user-a";
    var top = store.CreateGarment(CreateGarment(userId, "white tee", GarmentCategory.Top));
    var bottom = store.CreateGarment(CreateGarment(userId, "jeans", GarmentCategory.Bottom));
    var outfit = new OutfitService(store, store, new SystemClock())
        .CreateOutfit(userId, "casual", new[] { top.Id, bottom.Id });
    var provider = new CountingTryOnProvider();
    var service = new TryOnService(store, store, store, new RecordingTryOnJobQueue(), provider, new TryOnCostEstimator(), new SystemClock());
    var estimate = service.Estimate(userId, outfit.Id, TryOnMode.SequentialOutfitTryOn, "https://example.com/person.jpg", null);

    AssertThrows<InvalidOperationException>(
        () => service.StartAsync(userId, outfit.Id, "https://example.com/person.jpg", consentAccepted: true, TryOnMode.SequentialOutfitTryOn, confirmedCredits: 1, confirmedCacheKey: estimate.CacheKey).GetAwaiter().GetResult(),
        "confirmed credits must match server estimate");
    AssertThrows<InvalidOperationException>(
        () => service.StartAsync(userId, outfit.Id, "https://example.com/person.jpg", consentAccepted: true, TryOnMode.SequentialOutfitTryOn, confirmedCredits: estimate.EstimatedCredits, confirmedCacheKey: "stale-cache-key").GetAwaiter().GetResult(),
        "confirmed cache key must match server estimate");
    AssertEqual(0, provider.Calls, "confirmation mismatch must stop before provider work.");
}

static void TestTryOnServiceReturnsCacheHitsWithoutQueueing()
{
    var store = new InMemoryOutfitStore();
    var userId = "user-a";
    var top = store.CreateGarment(CreateGarment(userId, "white tee", GarmentCategory.Top));
    var outfit = new OutfitService(store, store, new SystemClock())
        .CreateOutfit(userId, "casual", new[] { top.Id });
    var provider = new CountingTryOnProvider();
    var queue = new RecordingTryOnJobQueue();
    var service = new TryOnService(store, store, store, queue, provider, new TryOnCostEstimator(), new SystemClock());
    var estimate = service.Estimate(userId, outfit.Id, TryOnMode.SingleGarmentTryOn, "https://example.com/person.jpg", null);
    var cached = new TryOnJob(Guid.NewGuid(), userId, outfit.Id, "https://example.com/person.jpg", false, TryOnStatus.Succeeded, "cached-provider-job", "https://example.com/cached.jpg", null, DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddMinutes(-5))
    {
        ProviderName = provider.Name,
        TryOnMode = TryOnMode.SingleGarmentTryOn,
        ConfirmedCredits = estimate.EstimatedCredits,
        CacheKey = estimate.CacheKey,
        ProviderSettingsHash = provider.Capabilities.SettingsHash
    };
    store.AddTryOnJob(cached);

    var job = service.StartAsync(userId, outfit.Id, "https://example.com/person.jpg", consentAccepted: true, TryOnMode.SingleGarmentTryOn, estimate.EstimatedCredits, estimate.CacheKey)
        .GetAwaiter()
        .GetResult();
    var updatedOutfit = new OutfitService(store, store, new SystemClock()).GetOutfit(userId, outfit.Id);

    AssertEqual(TryOnStatus.Succeeded, job.Status, "cache hit should return a succeeded job.");
    AssertTrue(job.ServedFromCache, "cache hit job should record cache provenance.");
    AssertEqual(cached.Id, job.SourceCachedJobId, "cache hit should link to source job.");
    AssertEqual("https://example.com/cached.jpg", job.OutputImageUrl, "cache hit should reuse output.");
    AssertEqual("https://example.com/cached.jpg", updatedOutfit?.PersonPreviewUrl, "cache hit should update outfit preview.");
    AssertEqual(0, queue.Enqueued.Count, "cache hit should not enqueue work.");
    AssertEqual(0, provider.Calls, "cache hit should not call provider.");
}

static void TestTryOnServiceDeletesActivePreviewOutputFromOutfit()
{
    var store = new InMemoryOutfitStore();
    var userId = "user-a";
    var top = store.CreateGarment(CreateGarment(userId, "white tee", GarmentCategory.Top));
    var outfit = new OutfitService(store, store, new SystemClock())
        .CreateOutfit(userId, "casual", new[] { top.Id });
    var provider = new CountingTryOnProvider();
    var service = new TryOnService(store, store, store, new RecordingTryOnJobQueue(), provider, new TryOnCostEstimator(), new SystemClock());
    var estimate = service.Estimate(userId, outfit.Id, TryOnMode.SequentialOutfitTryOn, "https://example.com/person.jpg", null);
    var job = service.StartAsync(userId, outfit.Id, "https://example.com/person.jpg", consentAccepted: true, TryOnMode.SequentialOutfitTryOn, estimate.EstimatedCredits, estimate.CacheKey)
        .GetAwaiter()
        .GetResult();

    service.ProcessQueuedJobAsync(job.Id).GetAwaiter().GetResult();
    AssertEqual("https://example.com/output.jpg", new OutfitService(store, store, new SystemClock()).GetOutfit(userId, outfit.Id)?.PersonPreviewUrl, "processed job should become the active outfit preview.");

    var deleted = service.DeleteOutput(userId, job.Id);
    var deletedJob = service.GetJob(userId, job.Id);
    var updatedOutfit = new OutfitService(store, store, new SystemClock()).GetOutfit(userId, outfit.Id);

    AssertTrue(deleted, "deleting a stored preview output should report success.");
    AssertTrue(deletedJob?.OutputImageUrl is null, "deleted try-on job should no longer expose an output image.");
    AssertTrue(deletedJob?.IsDeleted == true, "deleted try-on job should be marked deleted.");
    AssertTrue(updatedOutfit?.PersonPreviewUrl is null, "deleting the active preview output should clear the outfit person preview.");
}

static void TestTryOnServiceDeletesActivePreviewOutputByOutfit()
{
    var store = new InMemoryOutfitStore();
    var userId = "user-a";
    var top = store.CreateGarment(CreateGarment(userId, "white tee", GarmentCategory.Top));
    var outfit = new OutfitService(store, store, new SystemClock())
        .CreateOutfit(userId, "casual", new[] { top.Id });
    var provider = new CountingTryOnProvider();
    var service = new TryOnService(store, store, store, new RecordingTryOnJobQueue(), provider, new TryOnCostEstimator(), new SystemClock());
    var estimate = service.Estimate(userId, outfit.Id, TryOnMode.SequentialOutfitTryOn, "https://example.com/person.jpg", null);
    var job = service.StartAsync(userId, outfit.Id, "https://example.com/person.jpg", consentAccepted: true, TryOnMode.SequentialOutfitTryOn, estimate.EstimatedCredits, estimate.CacheKey)
        .GetAwaiter()
        .GetResult();

    service.ProcessQueuedJobAsync(job.Id).GetAwaiter().GetResult();

    var deleted = service.DeleteActiveOutfitOutput(userId, outfit.Id);
    var deletedJob = service.GetJob(userId, job.Id);
    var updatedOutfit = new OutfitService(store, store, new SystemClock()).GetOutfit(userId, outfit.Id);

    AssertTrue(deleted, "deleting the active outfit preview should report success.");
    AssertTrue(deletedJob?.OutputImageUrl is null, "matching try-on job should no longer expose an output image.");
    AssertTrue(deletedJob?.IsDeleted == true, "matching try-on job should be marked deleted.");
    AssertTrue(updatedOutfit?.PersonPreviewUrl is null, "deleting by outfit should clear the outfit person preview.");
}

static void TestTryOnServiceSendsCutoutGarmentImageToProvider()
{
    var store = new InMemoryOutfitStore();
    var userId = "user-a";
    var top = store.CreateGarment(CreateGarment(userId, "white tee", GarmentCategory.Top));
    var outfit = new OutfitService(store, store, new SystemClock())
        .CreateOutfit(userId, "casual", new[] { top.Id });
    var provider = new CountingTryOnProvider();
    var service = new TryOnService(
        store,
        store,
        store,
        new RecordingTryOnJobQueue(),
        provider,
        new TryOnCostEstimator(),
        new SystemClock(),
        new StubGarmentUrlRefresher());
    var estimate = service.Estimate(userId, outfit.Id, TryOnMode.SequentialOutfitTryOn, "https://example.com/person.jpg", null);
    var job = service.StartAsync(userId, outfit.Id, "https://example.com/person.jpg", consentAccepted: true, TryOnMode.SequentialOutfitTryOn, estimate.EstimatedCredits, estimate.CacheKey)
        .GetAwaiter()
        .GetResult();

    service.ProcessQueuedJobAsync(job.Id).GetAwaiter().GetResult();

    AssertEqual(1, provider.Calls, "queued AI job should call the provider once.");
    AssertEqual(
        "https://cdn.test/cutout.png",
        provider.LastRequest?.BodyTryOnItems[0].ThumbnailUrl,
        "provider should receive the high-res cutout garment image, not the 512px thumbnail.");
}

static void TestTryOnServiceCompletesClothesOnlyWithoutAi()
{
    var store = new InMemoryOutfitStore();
    var userId = "user-a";
    var top = store.CreateGarment(CreateGarment(userId, "white tee", GarmentCategory.Top));
    var outfit = new OutfitService(store, store, new SystemClock())
        .CreateOutfit(userId, "casual", new[] { top.Id });
    var provider = new CountingTryOnProvider();
    var queue = new RecordingTryOnJobQueue();
    var service = new TryOnService(store, store, store, queue, provider, new TryOnCostEstimator(), new SystemClock());
    var estimate = service.Estimate(userId, outfit.Id, TryOnMode.ClothesOnlyPreview, "https://example.com/person.jpg", null);

    var job = service.StartAsync(userId, outfit.Id, "https://example.com/person.jpg", consentAccepted: false, TryOnMode.ClothesOnlyPreview, estimate.EstimatedCredits, estimate.CacheKey)
        .GetAwaiter()
        .GetResult();

    AssertEqual(TryOnStatus.Succeeded, job.Status, "clothes-only preview should complete synchronously.");
    AssertEqual(0, job.ConfirmedCredits, "clothes-only preview should be free.");
    AssertEqual(0, queue.Enqueued.Count, "clothes-only preview should not enqueue provider work.");
    AssertEqual(0, provider.Calls, "clothes-only preview should not call provider.");
}

static void TestTryOnServiceCompletesClothesOnlyWithoutBodyReference()
{
    var store = new InMemoryOutfitStore();
    var userId = "user-a";
    var top = store.CreateGarment(CreateGarment(userId, "white tee", GarmentCategory.Top));
    var outfit = new OutfitService(store, store, new SystemClock())
        .CreateOutfit(userId, "casual", new[] { top.Id });
    var provider = new CountingTryOnProvider();
    var queue = new RecordingTryOnJobQueue();
    var service = new TryOnService(store, store, store, queue, provider, new TryOnCostEstimator(), new SystemClock());
    var estimate = service.Estimate(userId, outfit.Id, TryOnMode.ClothesOnlyPreview, "", null);

    var job = service.StartAsync(userId, outfit.Id, "", consentAccepted: false, TryOnMode.ClothesOnlyPreview, estimate.EstimatedCredits, estimate.CacheKey)
        .GetAwaiter()
        .GetResult();

    AssertTrue(estimate.IsAvailable, "clothes-only estimate should be available without a body reference.");
    AssertEqual(0, estimate.EstimatedCredits, "clothes-only estimate should remain free.");
    AssertEqual(TryOnStatus.Succeeded, job.Status, "clothes-only preview should complete without a body reference.");
    AssertEqual(0, queue.Enqueued.Count, "clothes-only preview without a body reference should not enqueue provider work.");
    AssertEqual(0, provider.Calls, "clothes-only preview without a body reference should not call provider.");
}

static void TestTryOnServiceQueuesJobsWithoutInlineProviderCall()
{
    var store = new InMemoryOutfitStore();
    var userId = "user-a";
    var top = store.CreateGarment(CreateGarment(userId, "white tee", GarmentCategory.Top));
    var outfit = new OutfitService(store, store, new SystemClock())
        .CreateOutfit(userId, "casual", new[] { top.Id });
    var provider = new CountingTryOnProvider();
    var queue = new RecordingTryOnJobQueue();
    var service = new TryOnService(store, store, store, queue, provider, new TryOnCostEstimator(), new SystemClock());
    var estimate = service.Estimate(userId, outfit.Id, TryOnMode.SingleGarmentTryOn, "https://example.com/person.jpg", null);

    var job = service.StartAsync(userId, outfit.Id, "https://example.com/person.jpg", consentAccepted: true, TryOnMode.SingleGarmentTryOn, estimate.EstimatedCredits, estimate.CacheKey)
        .GetAwaiter()
        .GetResult();

    AssertEqual(TryOnStatus.Queued, job.Status, "starting try-on should create a queued job.");
    AssertTrue(job.ConsentAcceptedAt is not null, "try-on jobs should record when consent was accepted.");
    AssertEqual("test", job.ProviderName, "try-on jobs should record provider name.");
    AssertTrue(job.RetentionUntil > job.CreatedAt, "try-on jobs should have an output retention deadline.");
    AssertTrue(!job.IsDeleted, "new try-on jobs should not be marked deleted.");
    AssertEqual(0, provider.Calls, "provider should not be called inline by the request path.");
    AssertEqual(1, queue.Enqueued.Count, "queued job id should be pushed to the background queue.");
    AssertEqual(job.Id, queue.Enqueued[0], "queued id should match the persisted job.");
}

static void TestTryOnProcessorCompletesQueuedJobs()
{
    var store = new InMemoryOutfitStore();
    var userId = "user-a";
    var top = store.CreateGarment(CreateGarment(userId, "white tee", GarmentCategory.Top));
    var outfit = new OutfitService(store, store, new SystemClock())
        .CreateOutfit(userId, "casual", new[] { top.Id });
    var provider = new CountingTryOnProvider();
    var queue = new RecordingTryOnJobQueue();
    var service = new TryOnService(store, store, store, queue, provider, new TryOnCostEstimator(), new SystemClock());
    var estimate = service.Estimate(userId, outfit.Id, TryOnMode.SequentialOutfitTryOn, "https://example.com/person.jpg", null);
    var job = service.StartAsync(userId, outfit.Id, "https://example.com/person.jpg", consentAccepted: true, TryOnMode.SequentialOutfitTryOn, estimate.EstimatedCredits, estimate.CacheKey)
        .GetAwaiter()
        .GetResult();

    service.ProcessQueuedJobAsync(job.Id).GetAwaiter().GetResult();
    var completed = service.GetJob(userId, job.Id);
    var updatedOutfit = new OutfitService(store, store, new SystemClock()).GetOutfit(userId, outfit.Id);

    AssertEqual(1, provider.Calls, "worker processing should call provider once.");
    AssertTrue(provider.LastRequest?.Mode == TryOnMode.SequentialOutfitTryOn, "worker should preserve sequential flow option from the queued job.");
    AssertEqual(TryOnStatus.Succeeded, completed?.Status, "processed job should succeed.");
    AssertEqual("https://example.com/output.jpg", completed?.OutputImageUrl, "processed job should store provider output.");
    AssertEqual("https://example.com/output.jpg", updatedOutfit?.PersonPreviewUrl, "processed job should update outfit preview.");
}

static void TestTryOnProcessorSendsPublicStorageUrlsToProvider()
{
    var tempPath = Path.Combine(Path.GetTempPath(), "outfit-planner-provider-url-tests", Guid.NewGuid().ToString("N"));

    try
    {
        var objects = new LocalObjectStorage(tempPath, "test-signing-key");
        PutTestObject(objects, "body-reference-photos/original/person.png");
        PutTestObject(objects, "garments/processed-cutout/shirt.png");
        var refresher = new StoredPhotoUrlRefresher(objects, "https://outfitplanner.net");
        var store = new InMemoryOutfitStore();
        var userId = "user-a";
        var bodyUrl = objects.CreateSignedReadUrl("body-reference-photos/original/person.png", TimeSpan.FromMinutes(15));
        var garmentUrl = objects.CreateSignedReadUrl("garments/processed-cutout/shirt.png", TimeSpan.FromMinutes(15));
        var top = store.CreateGarment(new CreateGarmentCommand(
            userId,
            "white tee",
            GarmentCategory.Top,
            garmentUrl,
            garmentUrl,
            Array.Empty<string>()));
        var outfit = new OutfitService(store, store, new SystemClock())
            .CreateOutfit(userId, "casual", new[] { top.Id });
        var provider = new CountingTryOnProvider();
        var service = new TryOnService(store, store, store, new RecordingTryOnJobQueue(), provider, new TryOnCostEstimator(), new SystemClock(), refresher);
        var estimate = service.Estimate(userId, outfit.Id, TryOnMode.SequentialOutfitTryOn, bodyUrl, null);
        var job = service.StartAsync(userId, outfit.Id, bodyUrl, consentAccepted: true, TryOnMode.SequentialOutfitTryOn, estimate.EstimatedCredits, estimate.CacheKey)
            .GetAwaiter()
            .GetResult();

        service.ProcessQueuedJobAsync(job.Id).GetAwaiter().GetResult();

        AssertTrue(provider.LastRequest?.BodyReferencePhotoUrl.StartsWith("https://outfitplanner.net/api/storage/signed/", StringComparison.Ordinal) == true, "provider body reference URL should be public and absolute.");
        AssertTrue(provider.LastRequest?.BodyTryOnItems.Single().ThumbnailUrl.StartsWith("https://outfitplanner.net/api/storage/signed/", StringComparison.Ordinal) == true, "provider garment URL should be public and absolute.");
    }
    finally
    {
        if (Directory.Exists(tempPath))
        {
            Directory.Delete(tempPath, recursive: true);
        }
    }
}

static void TestTryOnProcessorStoresExternalProviderOutputs()
{
    var store = new InMemoryOutfitStore();
    var userId = "user-a";
    var top = store.CreateGarment(CreateGarment(userId, "white tee", GarmentCategory.Top));
    var outfit = new OutfitService(store, store, new SystemClock())
        .CreateOutfit(userId, "casual", new[] { top.Id });
    var provider = new CountingTryOnProvider();
    var outputStorage = new RecordingTryOnOutputStorage("/api/storage/signed/try-on-output/job-output.png?expires=1&signature=test");
    var service = new TryOnService(store, store, store, new RecordingTryOnJobQueue(), provider, new TryOnCostEstimator(), new SystemClock(), tryOnOutputStorage: outputStorage);
    var estimate = service.Estimate(userId, outfit.Id, TryOnMode.SequentialOutfitTryOn, "https://example.com/person.jpg", null);
    var job = service.StartAsync(userId, outfit.Id, "https://example.com/person.jpg", consentAccepted: true, TryOnMode.SequentialOutfitTryOn, estimate.EstimatedCredits, estimate.CacheKey)
        .GetAwaiter()
        .GetResult();

    service.ProcessQueuedJobAsync(job.Id).GetAwaiter().GetResult();
    var completed = service.GetJob(userId, job.Id);
    var updatedOutfit = new OutfitService(store, store, new SystemClock()).GetOutfit(userId, outfit.Id);

    AssertEqual("https://example.com/output.jpg", outputStorage.LastSourceImageUrl, "processor should copy the provider output source.");
    AssertEqual(outputStorage.StoredUrl, completed?.OutputImageUrl, "processed job should expose the app-owned stored output url.");
    AssertEqual(outputStorage.StoredUrl, updatedOutfit?.PersonPreviewUrl, "outfit preview should use the app-owned stored output url.");
}

static void TestTryOnProcessorExcludesVisualOnlyItemsOutsideCompositeMode()
{
    var store = new InMemoryOutfitStore();
    var userId = "user-a";
    var top = store.CreateGarment(CreateGarment(userId, "white tee", GarmentCategory.Top));
    var bag = store.CreateGarment(CreateGarment(userId, "leather bag", GarmentCategory.Bag));
    var outfit = new OutfitService(store, store, new SystemClock())
        .CreateOutfit(userId, "casual", new[] { top.Id, bag.Id });
    var provider = new CountingTryOnProvider();
    var service = new TryOnService(store, store, store, new RecordingTryOnJobQueue(), provider, new TryOnCostEstimator(), new SystemClock());
    var estimate = service.Estimate(userId, outfit.Id, TryOnMode.SequentialOutfitTryOn, "https://example.com/person.jpg", null);
    var job = service.StartAsync(userId, outfit.Id, "https://example.com/person.jpg", consentAccepted: true, TryOnMode.SequentialOutfitTryOn, estimate.EstimatedCredits, estimate.CacheKey)
        .GetAwaiter()
        .GetResult();

    service.ProcessQueuedJobAsync(job.Id).GetAwaiter().GetResult();

    AssertEqual(1, provider.Calls, "provider should receive one queued request.");
    AssertEqual(1, provider.LastRequest?.BodyTryOnItems.Count ?? -1, "normal AI modes should send body try-on items.");
    AssertEqual(0, provider.LastRequest?.VisualOnlyItems.Count ?? -1, "normal AI modes must not send visual-only items to providers.");
}

static void TestTryOnServiceForwardsSequentialFlowOption()
{
    var store = new InMemoryOutfitStore();
    var userId = "user-a";
    var top = store.CreateGarment(CreateGarment(userId, "white tee", GarmentCategory.Top));
    var outfit = new OutfitService(store, store, new SystemClock())
        .CreateOutfit(userId, "casual", new[] { top.Id });
    var provider = new CountingTryOnProvider();
    var service = new TryOnService(store, store, store, new RecordingTryOnJobQueue(), provider, new TryOnCostEstimator(), new SystemClock());
    var estimate = service.Estimate(userId, outfit.Id, TryOnMode.SequentialOutfitTryOn, "https://example.com/person.jpg", null);

    var job = service.StartAsync(userId, outfit.Id, "https://example.com/person.jpg", consentAccepted: true, TryOnMode.SequentialOutfitTryOn, estimate.EstimatedCredits, estimate.CacheKey)
        .GetAwaiter()
        .GetResult();
    service.ProcessQueuedJobAsync(job.Id).GetAwaiter().GetResult();

    AssertEqual(1, provider.Calls, "provider should receive the try-on request");
    AssertTrue(provider.LastRequest?.Mode == TryOnMode.SequentialOutfitTryOn, "provider should receive sequential flow option");
}

static void TestTryOnProcessorPassesGenderToProvider()
{
    var store = new InMemoryOutfitStore();
    var userId = "user-a";
    EnsureTestUser(store, userId, UserGender.Female);
    var top = store.CreateGarment(CreateGarment(userId, "white tee", GarmentCategory.Top));
    var outfit = new OutfitService(store, store, new SystemClock())
        .CreateOutfit(userId, "casual", new[] { top.Id });
    var provider = new CountingTryOnProvider();
    var service = new TryOnService(store, store, store, new RecordingTryOnJobQueue(), provider, new TryOnCostEstimator(), new SystemClock());
    var estimate = service.Estimate(userId, outfit.Id, TryOnMode.SingleGarmentTryOn, "https://example.com/person.jpg", null);

    var job = service.StartAsync(userId, outfit.Id, "https://example.com/person.jpg", consentAccepted: true, TryOnMode.SingleGarmentTryOn, estimate.EstimatedCredits, estimate.CacheKey)
        .GetAwaiter()
        .GetResult();
    service.ProcessQueuedJobAsync(job.Id).GetAwaiter().GetResult();

    AssertEqual(UserGender.Female, provider.LastRequest?.UserGender, "provider request should include persisted user gender.");
}

static void TestApiRegistersRedisQueueAndProviderChoices()
{
    var programPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "OutfitPlanner.Api", "Program.cs"));
    var program = File.ReadAllText(programPath);

    AssertTrue(program.Contains("ConnectionStrings:Redis", StringComparison.Ordinal), "api should read Redis connection configuration.");
    AssertTrue(program.Contains("RedisTryOnJobQueue", StringComparison.Ordinal), "api should register a Redis-backed try-on queue when configured.");
    AssertTrue(program.Contains("InMemoryTryOnJobQueue", StringComparison.Ordinal), "api should keep a local in-memory queue fallback for development.");
    AssertTrue(program.Contains("LocalVton", StringComparison.Ordinal), "api should allow selecting the local VTON provider.");
    AssertTrue(program.Contains("LocalCatVton", StringComparison.Ordinal), "api should allow selecting the local CatVTON provider.");
    AssertTrue(program.Contains("Replicate", StringComparison.Ordinal), "api should allow selecting the Replicate provider.");
    AssertTrue(program.Contains("Fal", StringComparison.Ordinal), "api should allow selecting the Fal provider.");
    AssertTrue(program.Contains("compositefashntryonprovider", StringComparison.Ordinal), "api should allow selecting the composite FASHN provider by full class-name alias.");
    AssertTrue(program.Contains("selfhostedcatvtonprovider", StringComparison.Ordinal), "api should allow selecting the self-hosted CatVTON provider by full class-name alias.");
    AssertTrue(program.Contains("generalimageedittryonprovider", StringComparison.Ordinal), "api should allow selecting the general image edit provider by full class-name alias.");
    AssertTrue(program.Contains("backgroundRemovalProvider", StringComparison.Ordinal), "system status should expose the active background removal provider.");
    AssertTrue(program.Contains("backgroundRemovalConfiguredProvider", StringComparison.Ordinal), "system status should expose the configured background removal provider.");
}

static void TestApiMapsDotEnvFashnAliases()
{
    var programPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "OutfitPlanner.Api", "Program.cs"));
    var program = File.ReadAllText(programPath);

    AssertTrue(program.Contains("LoadDotEnvConfigurationAliases", StringComparison.Ordinal), "api startup should load supported aliases from the repository .env file.");

    foreach (var mapping in new[]
    {
        "(\"FASHN_API_KEY\", \"Fashn:ApiKey\")",
        "(\"FASHN_BASE_URL\", \"Fashn:BaseUrl\")",
        "(\"FASHN_MODEL_NAME\", \"Fashn:ModelName\")",
        "(\"FASHN_MODE\", \"Fashn:Mode\")",
        "(\"FASHN_NUM_SAMPLES\", \"Fashn:NumSamples\")",
        "(\"FASHN_OUTPUT_FORMAT\", \"Fashn:OutputFormat\")",
        "(\"FASHN_RETURN_BASE64\", \"Fashn:ReturnBase64\")",
        "(\"FASHN_SEGMENTATION_FREE\", \"Fashn:SegmentationFree\")",
        "(\"FASHN_GARMENT_PHOTO_TYPE\", \"Fashn:GarmentPhotoType\")",
        "(\"FASHN_SEED\", \"Fashn:Seed\")",
        "(\"FASHN_RESOLUTION\", \"Fashn:Resolution\")",
        "(\"FASHN_GENDER_PROMPT_TEMPLATE\", \"Fashn:GenderPromptTemplate\")"
    })
    {
        AssertTrue(program.Contains(mapping, StringComparison.Ordinal), $"api startup should map {mapping}.");
    }
}

static void TestDailySchedulePerUser()
{
    var store = new InMemoryOutfitStore();
    var outfitService = new OutfitService(store, store, new SystemClock());
    var scheduleService = new ScheduleService(store, store, new SystemClock());
    var day = new DateOnly(2026, 5, 21);
    var userATop = store.CreateGarment(CreateGarment("user-a", "white tee", GarmentCategory.Top));
    var userABottom = store.CreateGarment(CreateGarment("user-a", "jeans", GarmentCategory.Bottom));
    var userBTop = store.CreateGarment(CreateGarment("user-b", "black tee", GarmentCategory.Top));
    var userAFirstOutfit = outfitService.CreateOutfit("user-a", "casual", new[] { userATop.Id });
    var userASecondOutfit = outfitService.CreateOutfit("user-a", "casual with denim", new[] { userATop.Id, userABottom.Id });
    var userBOutfit = outfitService.CreateOutfit("user-b", "minimal", new[] { userBTop.Id });

    scheduleService.ScheduleOutfit("user-a", day, userAFirstOutfit.Id);
    scheduleService.ScheduleOutfit("user-a", day, userASecondOutfit.Id);
    scheduleService.ScheduleOutfit("user-b", day, userBOutfit.Id);

    AssertEqual(1, scheduleService.GetSchedule("user-a", day, day).Count, "same user should have one outfit for a day");
    AssertEqual(userASecondOutfit.Id, scheduleService.GetSchedule("user-a", day, day)[0].OutfitId, "latest schedule should replace same-day plan");
    AssertEqual(1, scheduleService.GetSchedule("user-b", day, day).Count, "other users should have independent schedules");
}

static void TestShareTokenGenerator()
{
    var generator = new SecureShareTokenGenerator();
    var tokens = Enumerable.Range(0, 100).Select(_ => generator.CreateToken()).ToList();

    AssertEqual(100, tokens.Distinct(StringComparer.Ordinal).Count(), "tokens should be unique in this sample");

    foreach (var token in tokens)
    {
        AssertTrue(token.Length >= 32, "token should have enough encoded entropy");
        AssertTrue(token.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_'), "token must be URL-safe");
    }
}

static void TestPhotoUploadRejectsUnsupportedContentType()
{
    var storage = new CountingPhotoStorage();
    var service = new PhotoUploadService(storage);

    AssertThrows<InvalidOperationException>(
        () => service.UploadGarmentPhoto(new IncomingPhoto("note.txt", "text/plain", 4, new MemoryStream(new byte[] { 1, 2, 3, 4 }))),
        "non-image uploads must be rejected");
    AssertEqual(0, storage.Calls, "invalid uploads must not reach storage");
}

static void TestPhotoUploadRejectsForgedImageContentType()
{
    var storage = new CountingPhotoStorage();
    var service = new PhotoUploadService(storage);

    AssertThrows<InvalidOperationException>(
        () => service.UploadGarmentPhoto(new IncomingPhoto("fake.png", "image/png", 11, new MemoryStream(Encoding.UTF8.GetBytes("not-an-image")))),
        "image uploads with forged MIME type must be rejected by magic bytes");
    AssertEqual(0, storage.Calls, "forged image uploads must not reach storage");
}

static void TestPhotoUploadAcceptsLargePhonePhotos()
{
    AssertTrue(PhotoUploadService.MaxPhotoBytes >= 50L * 1024 * 1024, "photo uploads should allow large phone photos.");
}

static void TestApiConfiguresUploadBodyLimits()
{
    var programPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "OutfitPlanner.Api", "Program.cs"));
    var program = File.ReadAllText(programPath);

    AssertTrue(program.Contains("MultipartBodyLengthLimit", StringComparison.Ordinal), "api should configure multipart upload body length.");
    AssertTrue(program.Contains("MaxRequestBodySize", StringComparison.Ordinal), "api should configure Kestrel request body length.");
}

static void TestApiExposesTestDiagnosticsAndTraceIds()
{
    var programPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "OutfitPlanner.Api", "Program.cs"));
    var program = File.ReadAllText(programPath);

    AssertTrue(program.Contains("X-Trace-Id", StringComparison.Ordinal), "api responses should include a trace id header.");
    AssertTrue(program.Contains("DetailedErrors", StringComparison.Ordinal), "api should gate detailed errors behind environment/config.");
    AssertTrue(program.Contains("Upload diagnostics", StringComparison.Ordinal), "upload endpoints should log diagnostics.");
    AssertTrue(program.Contains("Results.Json", StringComparison.Ordinal), "api should return structured JSON for unexpected errors in test/dev.");
}

static void TestObjectStoragePortsAndAdapters()
{
    AssertTrue(typeof(IObjectStorage).IsInterface, "application should expose an object storage port.");
    AssertTrue(typeof(LocalObjectStorage).GetInterfaces().Contains(typeof(IObjectStorage)), "local adapter should implement object storage.");
    AssertTrue(typeof(MinioObjectStorage).GetInterfaces().Contains(typeof(IObjectStorage)), "minio adapter should implement object storage.");
}

static void TestLocalObjectStorageEmitsPublicAbsoluteSignedUrls()
{
    var tempPath = Path.Combine(Path.GetTempPath(), "outfit-planner-object-url-tests", Guid.NewGuid().ToString("N"));

    try
    {
        var localOnly = new LocalObjectStorage(tempPath, "test-signing-key");
        var publicStorage = new LocalObjectStorage(tempPath, "test-signing-key", "https://outfitplanner.net");

        AssertTrue(localOnly.CreateSignedReadUrl("garments/original/shirt.png", TimeSpan.FromMinutes(5)).StartsWith("/api/storage/signed/", StringComparison.Ordinal), "local object storage should keep relative signed URLs without public origin.");
        AssertTrue(publicStorage.CreateSignedReadUrl("garments/original/shirt.png", TimeSpan.FromMinutes(5)).StartsWith("https://outfitplanner.net/api/storage/signed/", StringComparison.Ordinal), "public object storage should emit absolute signed URLs for external providers.");
    }
    finally
    {
        if (Directory.Exists(tempPath))
        {
            Directory.Delete(tempPath, recursive: true);
        }
    }
}

static void TestTryOnOutputStoragePortAndAdapter()
{
    AssertTrue(typeof(ITryOnOutputStorage).IsInterface, "application should expose a try-on output storage port.");
    AssertTrue(typeof(TryOnOutputStorage).GetInterfaces().Contains(typeof(ITryOnOutputStorage)), "infrastructure should store provider outputs behind the try-on output port.");
}

static void TestImageProcessingPipelineContracts()
{
    AssertTrue(typeof(IImageProcessor).IsInterface, "application should expose an image processing port.");
    var variantNames = Enum.GetNames<StoredImageVariant>();
    foreach (var variant in new[] { "Original", "Thumbnail", "ProcessedCutout", "TryOnOutput", "PrivatePreview", "SegmentationMask", "BaseCutout" })
    {
        AssertTrue(variantNames.Contains(variant), $"stored image variant {variant} should be modeled.");
    }
}

static void TestBackgroundRemovalProviderContracts()
{
    AssertTrue(typeof(IBackgroundRemovalProvider).IsInterface, "infrastructure should expose a background removal provider port.");
    AssertTrue(typeof(IGarmentExtractionProvider).IsInterface, "infrastructure should expose a garment extraction provider port.");
    AssertTrue(typeof(SimpleBackgroundRemovalProvider).GetInterfaces().Contains(typeof(IBackgroundRemovalProvider)), "simple cutout adapter should implement background removal.");
    AssertTrue(typeof(RembgBackgroundRemovalProvider).GetInterfaces().Contains(typeof(IBackgroundRemovalProvider)), "rembg adapter should implement background removal.");
    AssertTrue(typeof(RembgServerBackgroundRemovalProvider).GetInterfaces().Contains(typeof(IBackgroundRemovalProvider)), "rembg server adapter should implement background removal.");
    AssertTrue(typeof(HttpBackgroundRemovalProvider).GetInterfaces().Contains(typeof(IBackgroundRemovalProvider)), "http adapter should implement background removal.");
    AssertTrue(typeof(AutoBackgroundRemovalProvider).GetInterfaces().Contains(typeof(IBackgroundRemovalProvider)), "auto adapter should implement background removal.");
    AssertTrue(typeof(SingleGarmentExtractionProvider).GetInterfaces().Contains(typeof(IGarmentExtractionProvider)), "single item extraction adapter should implement garment extraction.");
    AssertTrue(
        typeof(ImageProcessor).GetConstructors().Any(constructor => constructor.GetParameters().Any(parameter => parameter.ParameterType == typeof(IGarmentExtractionProvider))),
        "image processor should accept a configured garment extraction provider.");
}

static void TestBackgroundRemovalAutoProviderPrefersRembg()
{
    var primary = new RecordingBackgroundRemovalProvider(MinimalPngBytes()) { ProviderName = "rembg" };
    var fallback = new RecordingBackgroundRemovalProvider(MinimalPngBytes()) { ProviderName = "simple" };
    var available = new AutoBackgroundRemovalProvider(primary, fallback, () => true);
    var unavailable = new AutoBackgroundRemovalProvider(primary, fallback, () => false);
    var request = new BackgroundRemovalRequest("shirt.png", "image/png", MinimalPngBytes());

    var availableResult = available.RemoveBackground(request);
    var unavailableResult = unavailable.RemoveBackground(request);

    AssertEqual("auto:rembg", available.Name, "auto provider should expose rembg as active when rembg is available");
    AssertEqual("rembg", availableResult.ProviderName, "auto provider should use rembg when available");
    AssertEqual(1, primary.Calls, "auto provider should call rembg once when available");
    AssertEqual("simple", unavailableResult.ProviderName, "auto provider should fall back to simple only when rembg is unavailable");
    AssertEqual(1, fallback.Calls, "auto provider should call fallback once when rembg is unavailable");
}

static void TestApiDefaultsBackgroundRemovalToAuto()
{
    var program = File.ReadAllText(Path.Combine("outfit_planner_back", "src", "OutfitPlanner.Api", "Program.cs"));

    AssertTrue(program.Contains("configuration[\"BackgroundRemoval:Provider\"] ?? \"Auto\"", StringComparison.Ordinal), "API should default background removal provider to auto.");
    AssertTrue(program.Contains("\"auto\" => new AutoBackgroundRemovalProvider", StringComparison.Ordinal), "API should wire auto background removal provider.");
}

static void TestImageProcessorDelegatesGarmentCutout()
{
    var provider = new RecordingBackgroundRemovalProvider(MinimalPngBytes());
    var processor = new ImageProcessor(provider);
    var bytes = MinimalPngBytes();

    var processed = processor.ProcessGarmentPhoto(new IncomingPhoto("shirt.png", "image/png", bytes.Length, new MemoryStream(bytes)));

    AssertEqual(1, provider.Calls, "garment processing should call the configured background remover once");
    AssertEqual("shirt.png", provider.LastRequest?.FileName, "background remover should receive the upload file name");
    AssertEqual("image/png", provider.LastRequest?.ContentType, "background remover should receive the normalized image content type");
    AssertTrue(provider.LastRequest?.ImageBytes.Length > 0, "background remover should receive image bytes");
    AssertTrue(processed.Images.Any(image => image.Variant == StoredImageVariant.ProcessedCutout && image.ContentType == "image/png"), "garment processing should store a png cutout variant");
    AssertTrue(processed.Images.Any(image => image.Variant == StoredImageVariant.SegmentationMask && image.ContentType == "image/png"), "garment processing should store a png segmentation mask");
}

static void TestImageProcessorEmitsBaseCutoutVariant()
{
    var processor = new ImageProcessor(new RecordingBackgroundRemovalProvider(MinimalPngBytes()));
    var bytes = MinimalPngBytes();

    var processed = processor.ProcessGarmentPhoto(new IncomingPhoto("shirt.png", "image/png", bytes.Length, new MemoryStream(bytes)));

    AssertTrue(
        processed.Images.Any(image => image.Variant == StoredImageVariant.BaseCutout && image.ContentType == "image/png"),
        "garment processing should emit an immutable base-cutout variant to rotate from later");
}

static void TestGarmentProcessingMeasuresAndTrimsCutout()
{
    // The same garment silhouette "shot" close up and far away: identical shape, different scale
    // and transparent padding around it.
    var closeUp = TransparentPaddedRectanglePng(400, 400, 100, 300);
    var farAway = TransparentPaddedRectanglePng(800, 800, 50, 150);

    var closeProcessed = new ImageProcessor(new RecordingBackgroundRemovalProvider(closeUp))
        .ProcessGarmentPhoto(new IncomingPhoto("shirt.png", "image/png", closeUp.Length, new MemoryStream(closeUp)));
    var farProcessed = new ImageProcessor(new RecordingBackgroundRemovalProvider(farAway))
        .ProcessGarmentPhoto(new IncomingPhoto("shirt.png", "image/png", farAway.Length, new MemoryStream(farAway)));

    AssertTrue(closeProcessed.CutoutMeasurement is not null && farProcessed.CutoutMeasurement is not null, "garment processing should measure the cutout");
    AssertEqual(100, closeProcessed.CutoutMeasurement!.WidthPx, "measurement should be the opaque bounding box width, not the padded frame");
    AssertEqual(300, closeProcessed.CutoutMeasurement.HeightPx, "measurement should be the opaque bounding box height, not the padded frame");

    var closeAspect = closeProcessed.CutoutMeasurement.HeightPx / (double)closeProcessed.CutoutMeasurement.WidthPx;
    var farAspect = farProcessed.CutoutMeasurement!.HeightPx / (double)farProcessed.CutoutMeasurement.WidthPx;
    AssertTrue(Math.Abs(closeAspect - farAspect) < 0.001, "the same garment shot closer or farther must measure the same aspect ratio");

    var cutout = closeProcessed.Images.First(image => image.Variant == StoredImageVariant.ProcessedCutout);
    using var cutoutImage = Image.Load<Rgba32>(cutout.Bytes);
    AssertEqual(100, cutoutImage.Width, "the stored cutout should be trimmed to its alpha bounding box");
    AssertEqual(300, cutoutImage.Height, "the stored cutout should be trimmed to its alpha bounding box");
}

static void TestGarmentCutoutCropsPastScatteredBackgroundGrain()
{
    // End-to-end through the REAL SimpleBackgroundRemovalProvider (the default dev keyer): an
    // 800x900 opaque tan floor, garment 300x360 centred, and 6 scattered dark grain streaks the
    // corner-key cannot remove (far from tan) sitting near the frame edges. A naive min/max box
    // would balloon to the full frame; the connected-component trim must crop to the garment.
    //
    // The source is encoded WITHOUT an alpha channel (ColorType.Rgb) on purpose: that is what every
    // real (JPEG/opaque) photo becomes, and it is the exact condition that used to make the keyer's
    // transparency get dropped on re-encode, leaving a full-frame opaque cutout.
    const int CW = 800, CH = 900, GW = 300, GH = 360;
    var gx0 = (CW - GW) / 2;
    var gy0 = (CH - GH) / 2;
    var streaks = new[] { (60, 120, 40, 4), (700, 200, 4, 50), (120, 760, 60, 4), (680, 720, 4, 40), (400, 60, 50, 4), (60, 450, 4, 60) };
    bool InStreak(int x, int y)
    {
        foreach (var (sx, sy, sw, sh) in streaks)
        {
            if (x >= sx && x < sx + sw && y >= sy && y < sy + sh) return true;
        }
        return false;
    }
    using var src = new Image<Rgba32>(CW, CH);
    var floor = new Rgba32(196, 178, 150, 255);
    var garment = new Rgba32(30, 34, 52, 255);
    var grain = new Rgba32(70, 55, 38, 255);
    src.ProcessPixelRows(accessor =>
    {
        for (var y = 0; y < accessor.Height; y++)
        {
            var row = accessor.GetRowSpan(y);
            for (var x = 0; x < row.Length; x++)
            {
                row[x] = (x >= gx0 && x < gx0 + GW && y >= gy0 && y < gy0 + GH)
                    ? garment
                    : InStreak(x, y) ? grain : floor;
            }
        }
    });
    using var ms = new MemoryStream();
    src.Save(ms, new SixLabors.ImageSharp.Formats.Png.PngEncoder { ColorType = SixLabors.ImageSharp.Formats.Png.PngColorType.Rgb });
    var bytes = ms.ToArray();

    var processor = new ImageProcessor(new SimpleBackgroundRemovalProvider());
    var processed = processor.ProcessGarmentPhoto(new IncomingPhoto("floor.png", "image/png", bytes.Length, new MemoryStream(bytes)));

    // Garment 300x360 on 800x900 upscaled to max 1600 -> ~534x640. Full frame would be ~1422x1600.
    var measurement = processed.CutoutMeasurement;
    AssertTrue(measurement is not null, "the garment cutout should measure");
    AssertTrue(
        measurement!.WidthPx < 700 && measurement.HeightPx < 800,
        $"scattered background grain must not balloon the cutout to the full frame (got {measurement.WidthPx}x{measurement.HeightPx}, garment is ~534x640)");
    AssertTrue(
        measurement.WidthPx > 400 && measurement.HeightPx > 500,
        $"the cutout must still contain the whole garment, not crop into it (got {measurement.WidthPx}x{measurement.HeightPx})");
    var aspect = measurement.HeightPx / (double)measurement.WidthPx;
    AssertTrue(Math.Abs(aspect - (360.0 / 300.0)) < 0.05, $"the cropped garment should keep its 1.2 aspect (got {aspect:0.00})");
}

static void TestSimpleBackgroundRemovalPreservesAlphaOnOpaqueSource()
{
    // The bug this guards: SimpleBackgroundRemovalProvider loads the image, writes alpha=0 to the
    // background, then re-encodes. When the SOURCE PNG had no alpha channel (every JPEG/opaque photo),
    // a bare PngEncoder inherits the "no alpha" hint and drops the transparency that was just computed,
    // so the cutout comes back fully opaque and the garment measures as the whole frame.
    using var src = new Image<Rgba32>(200, 200);
    src.ProcessPixelRows(a =>
    {
        for (var y = 0; y < a.Height; y++)
        {
            var r = a.GetRowSpan(y);
            for (var x = 0; x < r.Length; x++)
            {
                r[x] = (x is >= 60 and < 140 && y is >= 60 and < 140)
                    ? new Rgba32(20, 20, 20, 255)
                    : new Rgba32(210, 210, 210, 255);
            }
        }
    });
    using var ms = new MemoryStream();
    src.Save(ms, new SixLabors.ImageSharp.Formats.Png.PngEncoder { ColorType = SixLabors.ImageSharp.Formats.Png.PngColorType.Rgb });
    var opaqueSource = ms.ToArray();

    var result = new SimpleBackgroundRemovalProvider()
        .RemoveBackground(new BackgroundRemovalRequest("photo.png", "image/png", opaqueSource));

    using var keyed = Image.Load<Rgba32>(result.ImageBytes);
    var transparent = 0;
    keyed.ProcessPixelRows(a =>
    {
        for (var y = 0; y < a.Height; y++)
        {
            var r = a.GetRowSpan(y);
            for (var x = 0; x < r.Length; x++)
            {
                if (r[x].A < 16) transparent++;
            }
        }
    });
    AssertTrue(transparent > 0, "background removal of an opaque-source image must keep the computed transparency (not re-encode it away)");
}

static void TestMeasureGarmentCutoutIsScaleInvariant()
{
    // MeasureGarmentCutout is what the startup backfill runs on stored cutouts/originals.
    var processor = new ImageProcessor(new RecordingBackgroundRemovalProvider(MinimalPngBytes()));

    var tight = processor.MeasureGarmentCutout(TransparentPaddedRectanglePng(120, 320, 100, 300));
    var padded = processor.MeasureGarmentCutout(TransparentPaddedRectanglePng(640, 640, 200, 600));
    AssertTrue(tight is not null && padded is not null, "padded cutouts should measure");
    AssertEqual(100, tight!.WidthPx, "measurement should ignore transparent padding");
    AssertEqual(300, tight.HeightPx, "measurement should ignore transparent padding");
    AssertEqual(200, padded!.WidthPx, "a larger shot of the same shape should scale the raw pixels");
    AssertEqual(600, padded.HeightPx, "a larger shot of the same shape should scale the raw pixels");
    AssertTrue(
        Math.Abs(tight.HeightPx / (double)tight.WidthPx - padded.HeightPx / (double)padded.WidthPx) < 0.001,
        "aspect ratio must be invariant to shooting distance and padding");

    // Legacy fallback: an image without transparency (e.g. a stored original) measures as its
    // full frame instead of failing.
    using var opaqueImage = new Image<Rgba32>(240, 180);
    using var jpegStream = new MemoryStream();
    opaqueImage.Save(jpegStream, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder());
    var opaque = processor.MeasureGarmentCutout(jpegStream.ToArray());
    AssertEqual(240, opaque!.WidthPx, "an image without alpha should measure as its full frame");
    AssertEqual(180, opaque.HeightPx, "an image without alpha should measure as its full frame");

    AssertTrue(processor.MeasureGarmentCutout(Array.Empty<byte>()) is null, "empty input should not measure");
    AssertTrue(
        processor.MeasureGarmentCutout(TransparentPaddedRectanglePng(50, 50, 0, 0)) is null,
        "a fully transparent image should not measure");
}

static void TestCutoutTrimIgnoresScatteredBackgroundSpecks()
{
    var processor = new ImageProcessor(new RecordingBackgroundRemovalProvider(MinimalPngBytes()));

    // A 200x300 garment centred on a 600x800 frame, with scattered opaque specks (leftover
    // background the keyer missed) in the corners and mid-edges. A naive min/max bounding box
    // would balloon out to the specks; the measurement must instead hug the garment mass.
    var measurement = processor.MeasureGarmentCutout(GarmentWithScatteredSpecksPng(600, 800, 200, 300));

    AssertTrue(measurement is not null, "a garment with stray specks should still measure");
    AssertEqual(200, measurement!.WidthPx, "stray background specks must not inflate the measured width");
    AssertEqual(300, measurement.HeightPx, "stray background specks must not inflate the measured height");
}

static void TestGarmentDeskewStraightensTiltedSilhouette()
{
    var processor = new ImageProcessor(new RecordingBackgroundRemovalProvider(MinimalPngBytes()));
    var tilted = TiltedRectanglePng(420, 420, 90, 260, 15);

    var correction = processor.ComputeGarmentDeskewAngle(tilted);
    AssertTrue(
        Math.Abs(correction) > 5d && Math.Abs(correction) <= 30d,
        $"deskew should detect the ~15deg tilt within the cap (got {correction:0.0})");

    var rendered = processor.RenderRotatedGarment(tilted, correction);
    var residual = processor.ComputeGarmentDeskewAngle(rendered.CutoutPng);
    AssertTrue(
        Math.Abs(residual) < Math.Abs(correction),
        $"applying the correction should reduce the tilt (residual {residual:0.0} vs correction {correction:0.0})");
    AssertTrue(
        Math.Abs(residual) < 5d,
        $"straightened silhouette should be near upright (residual {residual:0.0})");
}

static void TestGarmentDeskewSkipsSquareAndExtremeTilt()
{
    var processor = new ImageProcessor(new RecordingBackgroundRemovalProvider(MinimalPngBytes()));

    var square = TiltedRectanglePng(420, 420, 200, 200, 12);
    AssertEqual(0d, processor.ComputeGarmentDeskewAngle(square), "near-square silhouette has no dominant axis, so it should not be auto-straightened");

    var extreme = TiltedRectanglePng(560, 560, 90, 300, 45);
    AssertEqual(0d, processor.ComputeGarmentDeskewAngle(extreme), "a tilt beyond the cap should be skipped rather than over-rotated");
}

static void TestImageProcessorRendersRotatedGarmentVariants()
{
    var processor = new ImageProcessor(new RecordingBackgroundRemovalProvider(MinimalPngBytes()));
    var upright = TiltedRectanglePng(300, 420, 100, 300, 0);

    var rendered = processor.RenderRotatedGarment(upright, 90);

    using var cutout = Image.Load<Rgba32>(rendered.CutoutPng);
    AssertTrue(cutout.Width > cutout.Height, "rotating a tall cutout by 90deg should yield a wider-than-tall image");
    AssertTrue(rendered.ThumbnailPng.Length > 0, "rotation should render a thumbnail variant");
    AssertTrue(rendered.SegmentationMaskPng.Length > 0, "rotation should render a segmentation mask variant");
    AssertTrue(!string.IsNullOrEmpty(rendered.PerceptualHash), "rotation should compute a perceptual hash");

    // A non-axis-aligned rotation leaves transparent corners in the (tightly cropped) bounding box.
    var diagonal = processor.RenderRotatedGarment(upright, 30);
    using var diagonalCutout = Image.Load<Rgba32>(diagonal.CutoutPng);
    var hasTransparent = false;
    diagonalCutout.ProcessPixelRows(accessor =>
    {
        for (var y = 0; y < accessor.Height && !hasTransparent; y++)
        {
            var row = accessor.GetRowSpan(y);
            for (var x = 0; x < row.Length; x++)
            {
                if (row[x].A < 255)
                {
                    hasTransparent = true;
                    break;
                }
            }
        }
    });
    AssertTrue(hasTransparent, "a diagonal rotation should fill exposed corners with transparency");
}

static void TestHttpBackgroundRemovalProviderPostsMultipartImageWithApiKey()
{
    var handler = new RecordingBackgroundRemovalHandler(MinimalPngBytes());
    var provider = new HttpBackgroundRemovalProvider(
        new HttpClient(handler),
        new HttpBackgroundRemovalSettings(
            "https://api.test/remove-background",
            "secret-key",
            "X-Api-Key",
            "",
            "image_file",
            TimeSpan.FromSeconds(10)));

    var result = provider.RemoveBackground(new BackgroundRemovalRequest("shirt.png", "image/png", MinimalPngBytes()));

    AssertEqual("image/png", result.ContentType, "http background remover should return image content");
    AssertTrue(result.ImageBytes.Length > 0, "http background remover should return image bytes");
    AssertEqual(HttpMethod.Post, handler.Request?.Method, "http background remover should post to provider endpoint");
    AssertEqual("/remove-background", handler.Request?.RequestUri?.AbsolutePath, "http background remover should call configured endpoint path");
    AssertTrue(handler.Request?.Headers.TryGetValues("X-Api-Key", out var values) == true && values.Contains("secret-key"), "http background remover should send configured api key header");
    AssertTrue(handler.Body.Contains("image_file", StringComparison.Ordinal), "multipart body should use configured image field name");
    AssertTrue(handler.Body.Contains("shirt.png", StringComparison.Ordinal), "multipart body should preserve upload file name");
}

static void TestRembgServerProviderPostsMultipartFileField()
{
    var handler = new RecordingBackgroundRemovalHandler(MinimalPngBytes());
    var provider = new RembgServerBackgroundRemovalProvider(
        new HttpClient(handler),
        new RembgServerBackgroundRemovalSettings(
            "http://127.0.0.1:7000/api/remove",
            "file",
            "birefnet-general-lite",
            TimeSpan.FromSeconds(10)));

    var result = provider.RemoveBackground(new BackgroundRemovalRequest("shirt.png", "image/png", MinimalPngBytes()));

    AssertEqual("rembg-server", result.ProviderName, "rembg server provider should identify rembg server outputs.");
    AssertEqual("/api/remove", handler.Request?.RequestUri?.AbsolutePath, "rembg server provider should call the remove API.");
    AssertTrue(handler.Body.Contains("name=file", StringComparison.Ordinal), "rembg server provider should post the upload as multipart field file.");
    AssertTrue(handler.Body.Contains("name=model", StringComparison.Ordinal), "rembg server provider should send the selected model as a multipart field.");
    AssertTrue(handler.Body.Contains("birefnet-general-lite", StringComparison.Ordinal), "rembg server provider should preserve the configured model name.");
    AssertTrue(handler.Body.Contains("shirt.png", StringComparison.Ordinal), "rembg server provider should preserve the uploaded filename.");
}

static void TestApiRegistersRembgServerProvider()
{
    var program = File.ReadAllText(Path.Combine("outfit_planner_back", "src", "OutfitPlanner.Api", "Program.cs"));

    AssertTrue(program.Contains("rembgserver", StringComparison.Ordinal), "API should expose an explicit rembg server background removal provider.");
    AssertTrue(program.Contains("BackgroundRemoval:RembgServer:Endpoint", StringComparison.Ordinal), "API should read rembg server endpoint configuration.");
    AssertTrue(program.Contains("http://127.0.0.1:7000/api/remove", StringComparison.Ordinal), "API should default rembg server endpoint to local rembg server remove API.");
    AssertTrue(program.Contains("\"RembgServer\"", StringComparison.Ordinal) && program.Contains("\"ModelName\"", StringComparison.Ordinal), "API should read rembg server model configuration.");
}

static void TestSingleGarmentExtractionScaffoldReturnsOneCutout()
{
    var remover = new RecordingBackgroundRemovalProvider(MinimalPngBytes());
    var extractor = new SingleGarmentExtractionProvider(remover);

    var result = extractor.ExtractGarments(new GarmentExtractionRequest("shirt.png", "image/png", MinimalPngBytes()));

    AssertEqual("single-garment", result.ProviderName, "single garment scaffold should expose its provider name");
    AssertEqual(1, result.Items.Count, "single garment scaffold should always return one candidate");
    AssertEqual(1, remover.Calls, "single garment scaffold should delegate cutout creation to the background remover");
    AssertEqual("image/png", result.Items[0].ContentType, "single garment scaffold should return transparent image bytes");
    AssertEqual("Top", result.Items[0].SuggestedCategory, "single garment scaffold should use a neutral category placeholder for future review");
    AssertEqual(1m, result.Items[0].Confidence, "single garment scaffold should mark its assumed one-item result as high confidence");
}

static void TestPhotoUploadStoresGarmentPhoto()
{
    var tempPath = Path.Combine(Path.GetTempPath(), "outfit-planner-photo-tests", Guid.NewGuid().ToString("N"));

    try
    {
        var bytes = MinimalPngBytes();
        var storage = new LocalPhotoStorage(tempPath);
        var service = new PhotoUploadService(storage);

        var stored = service.UploadGarmentPhoto(new IncomingPhoto("shirt.png", "image/png", bytes.Length, new MemoryStream(bytes)));

        AssertTrue(stored.Url.StartsWith("/api/storage/signed/", StringComparison.Ordinal), "stored garment photos should be served through signed object URLs");
        AssertTrue(stored.Url.Contains("/processed-cutout/", StringComparison.Ordinal), "primary garment upload URL should point at the processed cutout");
        AssertTrue(stored.OriginalUrl?.Contains("/original/", StringComparison.Ordinal) == true, "garment upload should expose the original variant URL");
        AssertTrue(stored.ThumbnailUrl?.Contains("/thumbnail/", StringComparison.Ordinal) == true, "garment upload should expose the thumbnail variant URL");
        AssertTrue(stored.ProcessedCutoutUrl?.Contains("/processed-cutout/", StringComparison.Ordinal) == true, "garment upload should expose the cutout variant URL");
        AssertTrue(stored.SegmentationMaskUrl?.Contains("/segmentation-mask/", StringComparison.Ordinal) == true, "garment upload should expose the segmentation mask variant URL");
        AssertEqual("image/png", stored.ContentType, "stored content type should be preserved");
        AssertTrue(stored.Length > 0, "stored length should reflect processed object bytes");
        AssertTrue(File.Exists(Path.Combine(tempPath, "garments", "original", stored.FileName)), "original garment object should exist on disk");
        AssertTrue(File.Exists(Path.Combine(tempPath, "garments", "thumbnail", stored.FileName)), "thumbnail garment object should exist on disk");
        AssertTrue(File.Exists(Path.Combine(tempPath, "garments", "processed-cutout", stored.FileName)), "processed cutout garment object should exist on disk");
        AssertTrue(ImageHasTransparentPixel(Path.Combine(tempPath, "garments", "thumbnail", stored.FileName)), "garment thumbnail should be generated from the processed cutout.");
    }
    finally
    {
        if (Directory.Exists(tempPath))
        {
            Directory.Delete(tempPath, recursive: true);
        }
    }
}

static void TestStoredPhotoUrlRefresherRefreshesGarmentVariants()
{
    var tempPath = Path.Combine(Path.GetTempPath(), "outfit-planner-url-refresh-tests", Guid.NewGuid().ToString("N"));

    try
    {
        var objects = new LocalObjectStorage(tempPath, "test-signing-key");
        PutTestObject(objects, "garments/original/shirt.png");
        PutTestObject(objects, "garments/thumbnail/shirt.png");
        PutTestObject(objects, "garments/processed-cutout/shirt.png");
        var staleOriginalUrl = "/api/storage/signed/garments/original/shirt.png?expires=1&signature=stale";
        var refresher = new StoredPhotoUrlRefresher(objects);

        var refreshedCutout = refresher.RefreshGarmentImageUrl(staleOriginalUrl);
        var refreshedThumbnail = refresher.RefreshGarmentThumbnailUrl(staleOriginalUrl);

        AssertTrue(refreshedCutout.Contains("/garments/processed-cutout/shirt.png", StringComparison.Ordinal), "stale primary garment URL should refresh to the processed cutout when it exists");
        AssertTrue(refreshedThumbnail.Contains("/garments/thumbnail/shirt.png", StringComparison.Ordinal), "stale thumbnail garment URL should refresh to the thumbnail variant when it exists");
        AssertFalse(string.Equals(staleOriginalUrl, refreshedCutout, StringComparison.Ordinal), "refreshed cutout URL should replace the stale signed URL");
        AssertFalse(refreshedCutout.Contains("signature=stale", StringComparison.Ordinal), "refreshed cutout URL should get a new signature");
    }
    finally
    {
        if (Directory.Exists(tempPath))
        {
            Directory.Delete(tempPath, recursive: true);
        }
    }
}

static void TestWardrobeServicePurgesAllUserStoredPhotos()
{
    var tempPath = Path.Combine(Path.GetTempPath(), "outfit-planner-purge-tests", Guid.NewGuid().ToString("N"));

    try
    {
        var storage = new LocalPhotoStorage(tempPath);
        var store = new InMemoryOutfitStore();
        var service = new WardrobeService(store, store, new SystemClock(), storage);
        var uploader = new PhotoUploadService(storage);

        var garmentPhoto = uploader.UploadGarmentPhoto(new IncomingPhoto("shirt.png", "image/png", MinimalPngBytes().Length, new MemoryStream(MinimalPngBytes())));
        service.CreateGarment(new CreateGarmentCommand("user-a", "linen shirt", GarmentCategory.Top, garmentPhoto.Url, garmentPhoto.Url, Array.Empty<string>()));
        var bodyPhoto = uploader.UploadBodyReferencePhoto(new IncomingPhoto("body.png", "image/png", MinimalPngBytes().Length, new MemoryStream(MinimalPngBytes())));
        service.CreateBodyReferencePhoto("user-a", bodyPhoto.Url);

        var removed = service.PurgeUserStoredPhotos("user-a");

        AssertTrue(removed >= 2, "purge should remove the user's garment and body objects.");
        AssertTrue(!File.Exists(Path.Combine(tempPath, "garments", "original", garmentPhoto.FileName)), "account purge should remove the garment original object.");
        AssertTrue(!File.Exists(Path.Combine(tempPath, "body-reference-photos", "original", bodyPhoto.FileName)), "account purge should remove the body reference original object.");
        AssertTrue(!File.Exists(Path.Combine(tempPath, "body-reference-photos", "private-preview", bodyPhoto.FileName)), "account purge should remove the blurred body preview object.");
    }
    finally
    {
        if (Directory.Exists(tempPath))
        {
            Directory.Delete(tempPath, recursive: true);
        }
    }
}

static void TestGarmentRotationWorksOnNonFileObjectStorage()
{
    // A byte-only object store with no local file path (like S3/MinIO). GetObject returns null
    // on purpose, so this fails unless server-side reads go through OpenReadObject.
    var objects = new InMemoryByteObjectStorage();
    var storage = new LocalPhotoStorage(objects, new ImageProcessor());
    var bytes = MinimalPngBytes();
    var stored = storage.SaveGarmentPhoto(new IncomingPhoto("shirt.png", "image/png", bytes.Length, new MemoryStream(bytes)));

    var rotated = storage.RotateGarment(stored.Url, 90d);

    AssertTrue(rotated.ImageUrl.Contains("/garments/processed-cutout/", StringComparison.Ordinal), "rotation should re-render the cutout against a non-file (S3/MinIO-style) object store.");
    var fileName = Path.GetFileName(rotated.ImageUrl.Split('?', 2)[0]);
    using var baseStream = objects.OpenReadObject($"garments/base-cutout/{fileName}");
    AssertTrue(baseStream is not null, "the immutable base cutout should remain readable through the storage abstraction.");
}

static void TestPhotoUploadStoresBodyReferencePhoto()
{
    var tempPath = Path.Combine(Path.GetTempPath(), "outfit-planner-body-photo-tests", Guid.NewGuid().ToString("N"));

    try
    {
        var bytes = MinimalPngBytes();
        var storage = new LocalPhotoStorage(tempPath);
        var service = new PhotoUploadService(storage);

        var stored = service.UploadBodyReferencePhoto(new IncomingPhoto("body.png", "image/png", bytes.Length, new MemoryStream(bytes)));

        AssertTrue(stored.Url.StartsWith("/api/storage/signed/", StringComparison.Ordinal), "stored body photos should be served through signed object URLs");
        AssertEqual("image/png", stored.ContentType, "stored content type should be preserved");
        AssertTrue(File.Exists(Path.Combine(tempPath, "body-reference-photos", "original", stored.FileName)), "uploaded body photo should exist on disk");
        AssertTrue(File.Exists(Path.Combine(tempPath, "body-reference-photos", "thumbnail", stored.FileName)), "body photo thumbnail should exist on disk");
        AssertTrue(File.Exists(Path.Combine(tempPath, "body-reference-photos", "private-preview", stored.FileName)), "body photo private preview should exist on disk");
        AssertTrue(storage.GetBodyReferencePhoto(stored.FileName) is not null, "body photo reader should find stored file");
    }
    finally
    {
        if (Directory.Exists(tempPath))
        {
            Directory.Delete(tempPath, recursive: true);
        }
    }
}

static void TestWardrobeServiceDeletesGarmentAndStoredPhoto()
{
    var tempPath = Path.Combine(Path.GetTempPath(), "outfit-planner-delete-garment-tests", Guid.NewGuid().ToString("N"));

    try
    {
        var storage = new LocalPhotoStorage(tempPath);
        var store = new InMemoryOutfitStore();
        var service = new WardrobeService(store, store, new SystemClock(), storage);
        var stored = new PhotoUploadService(storage)
            .UploadGarmentPhoto(new IncomingPhoto("shirt.png", "image/png", MinimalPngBytes().Length, new MemoryStream(MinimalPngBytes())));
        var garment = service.CreateGarment(new CreateGarmentCommand(
            "user-a",
            "linen shirt",
            GarmentCategory.Top,
            stored.Url,
            stored.Url,
            Array.Empty<string>()));

        var deleted = service.DeleteGarment("user-a", garment.Id);

        AssertTrue(deleted, "existing garment should be deleted");
        AssertEqual(0, service.ListGarments("user-a").Count, "deleted garment should disappear from wardrobe");
        AssertTrue(!File.Exists(Path.Combine(tempPath, "garments", "original", stored.FileName)), "deleted garment should remove its original object");
        AssertTrue(!File.Exists(Path.Combine(tempPath, "garments", "thumbnail", stored.FileName)), "deleted garment should remove its thumbnail object");
        AssertTrue(!service.DeleteGarment("user-b", garment.Id), "other users must not delete the garment");
    }
    finally
    {
        if (Directory.Exists(tempPath))
        {
            Directory.Delete(tempPath, recursive: true);
        }
    }
}

static void TestWardrobeServiceDeletesBodyReferenceAndStoredPhoto()
{
    var tempPath = Path.Combine(Path.GetTempPath(), "outfit-planner-delete-body-tests", Guid.NewGuid().ToString("N"));

    try
    {
        var storage = new LocalPhotoStorage(tempPath);
        var store = new InMemoryOutfitStore();
        var service = new WardrobeService(store, store, new SystemClock(), storage);
        var stored = new PhotoUploadService(storage)
            .UploadBodyReferencePhoto(new IncomingPhoto("body.png", "image/png", MinimalPngBytes().Length, new MemoryStream(MinimalPngBytes())));
        var photo = service.CreateBodyReferencePhoto("user-a", stored.Url);

        var deleted = service.DeleteBodyReferencePhoto("user-a", photo.Id);

        AssertTrue(deleted, "existing body reference should be deleted");
        AssertEqual(0, service.ListBodyReferencePhotos("user-a").Count, "deleted body reference should disappear from the library");
        AssertTrue(!File.Exists(Path.Combine(tempPath, "body-reference-photos", "original", stored.FileName)), "deleted body reference should remove its original object");
        AssertTrue(!File.Exists(Path.Combine(tempPath, "body-reference-photos", "thumbnail", stored.FileName)), "deleted body reference should remove its thumbnail object");
        AssertTrue(!service.DeleteBodyReferencePhoto("user-b", photo.Id), "other users must not delete the body reference");
    }
    finally
    {
        if (Directory.Exists(tempPath))
        {
            Directory.Delete(tempPath, recursive: true);
        }
    }
}

static void TestPostgresSchemaContainsStructuredMetadataAndIndexes()
{
    var schemaPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "database", "schema.sql"));
    var schema = File.ReadAllText(schemaPath);

    foreach (var column in new[]
    {
        "primary_color",
        "secondary_colors",
        "material",
        "brand",
        "size",
        "season",
        "weather_min_temp",
        "weather_max_temp",
        "occasion",
        "formality_score",
        "warmth_score",
        "comfort_score",
        "is_favorite",
        "is_archived",
        "last_worn_at",
        "laundry_status"
    })
    {
        AssertTrue(schema.Contains(column, StringComparison.OrdinalIgnoreCase), $"schema should include garment metadata column {column}.");
    }

    AssertTrue(schema.Contains("ix_garment_items_user_category", StringComparison.OrdinalIgnoreCase), "schema should index garment user/category filters.");
    AssertTrue(schema.Contains("ix_garment_items_user_created_at", StringComparison.OrdinalIgnoreCase), "schema should index garment recent sorting.");
    AssertTrue(schema.Contains("ix_scheduled_outfits_user_date", StringComparison.OrdinalIgnoreCase), "schema should index schedule date lookup.");
    AssertTrue(schema.Contains("ix_outfits_user_created_at", StringComparison.OrdinalIgnoreCase), "schema should index outfit recent sorting.");
    AssertTrue(schema.Contains("using gin (tags)", StringComparison.OrdinalIgnoreCase), "schema should add a GIN index for garment tags.");
}

static void TestPostgresSchemaContainsCutoutMeasurementColumns()
{
    var basePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var schema = File.ReadAllText(Path.Combine(basePath, "database", "schema.sql"));
    var migration = File.ReadAllText(Path.Combine(basePath, "database", "migrations", "008_garment_cutout_measurement.sql"));

    foreach (var column in new[] { "cutout_width_px", "cutout_height_px" })
    {
        AssertTrue(schema.Contains(column, StringComparison.OrdinalIgnoreCase), $"schema.sql should include garment column {column}.");
        AssertTrue(migration.Contains(column, StringComparison.OrdinalIgnoreCase), $"migration 008 should add {column} so schema.sql and migrations stay in sync.");
    }
}

static void TestOutfitServicePersistsComposedFigureState()
{
    var store = new InMemoryOutfitStore();
    var wardrobe = new WardrobeService(store, store, new SystemClock());
    var catalog = new StubHairstylePresetCatalog(
        new HairstylePreset("male-short-1", UserGender.Male, "Short cut I", "male-short-1.svg", 1));
    var service = new OutfitService(store, store, new SystemClock(), catalog);

    var top = wardrobe.CreateGarment(CreateGarment("user-a", "linen shirt", GarmentCategory.Top));
    var outfit = service.CreateOutfit("user-a", "city walk", new[] { top.Id }, "male-short-1", hairstyleVisible: false, UserGender.Male);
    AssertEqual("male-short-1", outfit.HairstylePresetId, "create should persist the worn hairstyle preset");
    AssertTrue(!outfit.HairstyleVisible, "create should persist hairstyle visibility");
    AssertEqual(UserGender.Male, outfit.SilhouetteGender, "create should persist the silhouette gender");

    var reloaded = service.GetOutfit("user-a", outfit.Id);
    AssertEqual("male-short-1", reloaded!.HairstylePresetId, "composed state should round-trip through the store");

    var untouched = service.UpdateOutfit("user-a", outfit.Id, new UpdateOutfitCommand(Name: "renamed"));
    AssertEqual("male-short-1", untouched!.HairstylePresetId, "null hairstyle updates should leave the preset unchanged");
    AssertTrue(!untouched.HairstyleVisible, "null visibility updates should leave visibility unchanged");

    var shown = service.UpdateOutfit("user-a", outfit.Id, new UpdateOutfitCommand(HairstyleVisible: true, SilhouetteGender: UserGender.Female));
    AssertTrue(shown!.HairstyleVisible, "hairstyle visibility should be updatable");
    AssertEqual(UserGender.Female, shown.SilhouetteGender, "the silhouette gender should be updatable");

    var cleared = service.UpdateOutfit("user-a", outfit.Id, new UpdateOutfitCommand(HairstylePresetId: ""));
    AssertTrue(cleared!.HairstylePresetId is null, "an empty hairstyle preset id should clear the worn hairstyle");

    AssertThrows<ValidationException>(
        () => service.CreateOutfit("user-a", "bad", new[] { top.Id }, "unknown-style"),
        "unknown hairstyle presets should be rejected");
}

static void TestOutfitServiceKeepsPersonPreviewOnMetadataUpdate()
{
    var store = new InMemoryOutfitStore();
    var service = new OutfitService(store, store, new SystemClock());
    var top = store.CreateGarment(CreateGarment("user-a", "white tee", GarmentCategory.Top));
    var bottom = store.CreateGarment(CreateGarment("user-a", "blue jeans", GarmentCategory.Bottom));
    var outfit = service.CreateOutfit("user-a", "look", new[] { top.Id });

    // Simulate a generated try-on preview that a succeeded job saved onto the outfit.
    store.UpdateOutfit(store.GetOutfitByUser("user-a", outfit.Id)! with { PersonPreviewUrl = "https://example.com/preview.jpg" });

    // A metadata-only save that re-sends the same garment set (what the Builder does on rename/save)
    // must keep the generated preview.
    var renamed = service.UpdateOutfit("user-a", outfit.Id, new UpdateOutfitCommand(Name: "renamed", GarmentIds: new[] { top.Id }));
    AssertEqual("https://example.com/preview.jpg", renamed!.PersonPreviewUrl, "a save with an unchanged garment set should keep the generated preview.");

    // Changing the worn garments invalidates the stale preview.
    var recomposed = service.UpdateOutfit("user-a", outfit.Id, new UpdateOutfitCommand(GarmentIds: new[] { top.Id, bottom.Id }));
    AssertTrue(recomposed!.PersonPreviewUrl is null, "changing the worn garments should clear the stale generated preview.");
}

static void TestOutfitItemsCarryGarmentCutoutMeasurements()
{
    var store = new InMemoryOutfitStore();
    var wardrobe = new WardrobeService(store, store, new SystemClock());
    var service = new OutfitService(store, store, new SystemClock());

    var top = wardrobe.CreateGarment(CreateGarment("user-a", "linen shirt", GarmentCategory.Top) with
    {
        CutoutWidthPx = 400,
        CutoutHeightPx = 520
    });
    var outfit = service.CreateOutfit("user-a", "measured", new[] { top.Id });

    AssertEqual(400, outfit.Items[0].CutoutWidthPx, "outfit items should carry the garment cutout width");
    AssertEqual(520, outfit.Items[0].CutoutHeightPx, "outfit items should carry the garment cutout height");
}

static void TestPostgresSchemaContainsComposedFigureColumns()
{
    var basePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var schema = File.ReadAllText(Path.Combine(basePath, "database", "schema.sql"));
    var migration = File.ReadAllText(Path.Combine(basePath, "database", "migrations", "010_outfit_composed_figure.sql"));

    foreach (var column in new[] { "hairstyle_preset_id", "hairstyle_visible", "silhouette_gender" })
    {
        AssertTrue(schema.Contains(column, StringComparison.OrdinalIgnoreCase), $"schema.sql should include outfit column {column}.");
        AssertTrue(migration.Contains(column, StringComparison.OrdinalIgnoreCase), $"migration 010 should add {column} so schema.sql and migrations stay in sync.");
    }
}

static void TestHatCategoryFullyRetired()
{
    var basePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var schema = File.ReadAllText(Path.Combine(basePath, "database", "schema.sql"));
    var migration = File.ReadAllText(Path.Combine(basePath, "database", "migrations", "009_remove_hat_category.sql"));

    AssertTrue(!schema.Contains("'Hat'", StringComparison.Ordinal), "schema snapshot must not allow the retired Hat category.");
    AssertTrue(migration.Contains("delete from garment_items where category = 'Hat'", StringComparison.Ordinal), "migration 009 should delete legacy Hat garments outright.");
    AssertTrue(migration.Contains("garment_items_category_check", StringComparison.Ordinal), "migration 009 should rebuild the garment category constraint.");
    AssertTrue(migration.Contains("outfit_items_category_check", StringComparison.Ordinal), "migration 009 should rebuild the outfit item category constraint.");
}

static void TestPostgresSchemaContainsCascadeAndCleanupIndexes()
{
    var basePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var schema = File.ReadAllText(Path.Combine(basePath, "database", "schema.sql"));
    var migration = File.ReadAllText(Path.Combine(basePath, "database", "migrations", "006_add_indexes.sql"));

    foreach (var index in new[]
    {
        "ix_try_on_jobs_user_created_at",
        "ix_auth_sessions_expires_at",
        "ix_outfit_items_outfit_id",
        "ix_outfit_items_garment_id",
        "ix_scheduled_outfits_outfit_id",
        "ix_try_on_jobs_outfit_id",
        "ix_share_links_outfit_id",
        "ix_body_reference_photos_user_id"
    })
    {
        AssertTrue(schema.Contains(index, StringComparison.OrdinalIgnoreCase), $"schema.sql should declare {index} (cascade/cleanup index).");
        AssertTrue(migration.Contains(index, StringComparison.OrdinalIgnoreCase), $"migration 006 should declare {index} so schema.sql and migrations stay in sync.");
    }
}

static void TestPostgresSchemaContainsPrivacyStorageAuthAndRetentionFields()
{
    var schemaPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "database", "schema.sql"));
    var schema = File.ReadAllText(schemaPath);

    foreach (var value in new[]
    {
        "Dress",
        "Outerwear",
        "Shoes",
        "Bag",
        "Accessory",
        "FullBody",
        "Feet",
        "Head",
        "Hands",
        "OuterLayer"
    })
    {
        AssertTrue(schema.Contains(value, StringComparison.Ordinal), $"schema should allow enum value {value}.");
    }

    foreach (var column in new[]
    {
        "object_key",
        "thumbnail_object_key",
        "processed_cutout_object_key",
        "perceptual_hash",
        "email_verified_at",
        "two_factor_enabled",
        "auth_email_verification_tokens",
        "auth_password_reset_tokens",
        "consent_accepted_at",
        "provider_name",
        "provider_request_id",
        "source_body_photo_id",
        "retention_until",
        "is_deleted",
        "try_on_mode",
        "confirmed_credits",
        "cache_key",
        "served_from_cache",
        "source_cached_job_id",
        "provider_settings_hash"
    })
    {
        AssertTrue(schema.Contains(column, StringComparison.OrdinalIgnoreCase), $"schema should include privacy/storage/auth field {column}.");
    }
}

static void TestTryOnStoragePersistsModeCostAndCacheMetadata()
{
    var store = new InMemoryOutfitStore();
    var now = DateTimeOffset.UtcNow;
    var cached = new TryOnJob(
        Guid.NewGuid(),
        "user-a",
        Guid.NewGuid(),
        "https://example.com/person.jpg",
        SequentialFlowEnabled: false,
        TryOnStatus.Succeeded,
        "provider-job",
        "https://example.com/output.jpg",
        null,
        now,
        now)
    {
        ProviderName = "FashnTryOnProvider",
        TryOnMode = TryOnMode.SequentialOutfitTryOn,
        ConfirmedCredits = 2,
        CacheKey = "cache-key-a",
        ProviderSettingsHash = "settings-a",
        ServedFromCache = false,
        IsDeleted = false
    };
    var deleted = cached with
    {
        Id = Guid.NewGuid(),
        CacheKey = "cache-key-deleted",
        IsDeleted = true
    };

    store.AddTryOnJob(cached);
    store.AddTryOnJob(deleted);

    var hit = store.FindSucceededTryOnJobByCacheKey("user-a", "cache-key-a");
    var deletedHit = store.FindSucceededTryOnJobByCacheKey("user-a", "cache-key-deleted");

    AssertEqual(cached.Id, hit?.Id, "cache lookup should return the matching succeeded job.");
    AssertEqual(TryOnMode.SequentialOutfitTryOn, hit!.TryOnMode, "job should persist try-on mode.");
    AssertEqual(2, hit.ConfirmedCredits, "job should persist confirmed credits.");
    AssertEqual("settings-a", hit.ProviderSettingsHash, "job should persist provider settings hash.");
    AssertTrue(deletedHit is null, "deleted outputs must not be cache hits.");
}

static void TestApiUsesDbUpMigrations()
{
    var rootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var apiProject = File.ReadAllText(Path.Combine(rootPath, "src", "OutfitPlanner.Api", "OutfitPlanner.Api.csproj"));
    var program = File.ReadAllText(Path.Combine(rootPath, "src", "OutfitPlanner.Api", "Program.cs"));
    var migrationPath = Path.Combine(rootPath, "database", "migrations");

    AssertTrue(apiProject.Contains("dbup-postgresql", StringComparison.OrdinalIgnoreCase), "api project should reference DbUp PostgreSQL package.");
    AssertTrue(program.Contains("PostgresMigrationRunner", StringComparison.Ordinal), "api startup should run DbUp migrations.");
    AssertTrue(!program.Contains("PostgresSchemaInitializer", StringComparison.Ordinal), "api startup should no longer initialize schema.sql directly.");
    AssertTrue(Directory.Exists(migrationPath), "database migrations directory should exist.");
    AssertTrue(Directory.GetFiles(migrationPath, "*.sql").Length > 0, "database migrations directory should contain SQL migrations.");
    var archivedGarmentCleanup = File.ReadAllText(Path.Combine(migrationPath, "003_delete_archived_garments.sql"));
    AssertTrue(archivedGarmentCleanup.Contains("delete from garment_items", StringComparison.OrdinalIgnoreCase), "migrations should delete archived garment records.");
    AssertTrue(archivedGarmentCleanup.Contains("is_archived = true", StringComparison.OrdinalIgnoreCase), "archived garment cleanup should target archived rows only.");
}

static void TestProviderAdaptersImplementPort()
{
    var providerTypes = new[]
    {
        typeof(LocalVtonProvider),
        typeof(LocalCatVtonProvider),
        typeof(ReplicateProvider),
        typeof(FalProvider),
        typeof(FashnTryOnProvider),
        typeof(CompositeFashnTryOnProvider),
        typeof(SelfHostedCatVtonProvider),
        typeof(GeneralImageEditTryOnProvider),
        typeof(MockTryOnProvider)
    };

    foreach (var providerType in providerTypes)
    {
        AssertTrue(typeof(ITryOnProvider).IsAssignableFrom(providerType), $"{providerType.Name} should implement ITryOnProvider.");
    }
}

static void TestJsonProviderPayloadAndEndpointPath()
{
    var handler = new RecordingHttpProviderHandler();
    handler.EnqueueJson(HttpStatusCode.OK, "{\"provider_job_id\":\"provider-job-1\",\"output_image_url\":\"https://cdn.test/output.png\"}");
    var outfitId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    var bodyItemId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    var visualItemId = Guid.Parse("20000000-0000-0000-0000-000000000003");
    var provider = new CompositeFashnTryOnProvider(
        new HttpClient(handler),
        new HttpTryOnProviderSettings("https://api.test/v1/", "/try-on", "test-key", "composite-v1", RequiresApiKey: true));

    var generation = provider.Generate(new TryOnProviderRequest(
        "user-a",
        outfitId,
        TryOnMode.ExperimentalCompositeTryOn,
        "https://app.test/body.jpg",
        new[]
        {
            new OutfitItem(bodyItemId, "white tee", GarmentCategory.Top, BodyZone.Torso, "https://app.test/top.png")
        },
        new[]
        {
            new OutfitItem(visualItemId, "leather bag", GarmentCategory.Bag, BodyZone.Accessory, "https://app.test/bag.png")
        },
        new TryOnGenerationSettings("composite-v1", "experimental-composite", "settings-a")));

    AssertEqual("provider-job-1", generation.ProviderJobId, "json provider should return provider job id from response");
    AssertEqual("https://cdn.test/output.png", generation.OutputImageUrl, "json provider should return output image url from response");
    AssertEqual(1, handler.Requests.Count, "json provider should post one request");
    AssertEqual(HttpMethod.Post, handler.Requests[0].Method, "json provider should use POST");
    AssertEqual("/v1/try-on", handler.Requests[0].Path, "json provider should preserve the base URI path when endpoint starts with slash");
    AssertEqual("Bearer", handler.Requests[0].Authorization?.Scheme, "json provider should use bearer auth when api key is configured");
    AssertEqual("test-key", handler.Requests[0].Authorization?.Parameter, "json provider should send configured api key");

    using var body = JsonDocument.Parse(handler.Requests[0].Body);
    var root = body.RootElement;
    AssertEqual("composite-v1", root.GetProperty("model_name").GetString(), "json provider should send configured model");
    AssertEqual("user-a", root.GetProperty("user_id").GetString(), "json provider should send user id");
    AssertEqual(outfitId, root.GetProperty("outfit_id").GetGuid(), "json provider should send outfit id");
    AssertEqual("https://app.test/body.jpg", root.GetProperty("model_image").GetString(), "json provider should send body reference image");
    AssertEqual("ExperimentalCompositeTryOn", root.GetProperty("try_on_mode").GetString(), "json provider should send try-on mode");

    var bodyItems = root.GetProperty("body_try_on_items");
    AssertEqual(1, bodyItems.GetArrayLength(), "json provider should send body try-on item count");
    AssertEqual(bodyItemId, bodyItems[0].GetProperty("id").GetGuid(), "json provider should send body item id");
    AssertEqual("white tee", bodyItems[0].GetProperty("name").GetString(), "json provider should send body item name");
    AssertEqual("Top", bodyItems[0].GetProperty("category").GetString(), "json provider should send body item category");
    AssertEqual("https://app.test/top.png", bodyItems[0].GetProperty("image_url").GetString(), "json provider should send body item image");

    var visualItems = root.GetProperty("visual_only_items");
    AssertEqual(1, visualItems.GetArrayLength(), "json provider should send visual-only item count");
    AssertEqual(visualItemId, visualItems[0].GetProperty("id").GetGuid(), "json provider should send visual-only item id");
    AssertEqual("leather bag", visualItems[0].GetProperty("name").GetString(), "json provider should send visual-only item name");
    AssertEqual("Bag", visualItems[0].GetProperty("category").GetString(), "json provider should send visual-only item category");
    AssertEqual("https://app.test/bag.png", visualItems[0].GetProperty("image_url").GetString(), "json provider should send visual-only item image");
}

static void TestJsonProviderCapabilitiesAreModeSpecific()
{
    var settings = new HttpTryOnProviderSettings("https://api.test/v1/", "try-on", "test-key", "model-x", RequiresApiKey: true);
    var composite = new CompositeFashnTryOnProvider(new HttpClient(new RecordingHttpProviderHandler()), settings);
    var selfHosted = new SelfHostedCatVtonProvider(new HttpClient(new RecordingHttpProviderHandler()), settings);
    var imageEdit = new GeneralImageEditTryOnProvider(new HttpClient(new RecordingHttpProviderHandler()), settings);

    AssertSupportedModes(
        composite,
        new[] { TryOnMode.ExperimentalCompositeTryOn },
        "composite FASHN should only advertise composite mode");
    AssertEqual("experimental-composite", composite.Capabilities.ProviderMode, "composite FASHN should expose its provider mode");
    AssertEqual("model-x:experimental-composite", composite.Capabilities.SettingsHash, "composite FASHN settings hash should include explicit provider mode");

    AssertSupportedModes(
        selfHosted,
        new[] { TryOnMode.SingleGarmentTryOn, TryOnMode.SequentialOutfitTryOn },
        "self-hosted CatVTON should only advertise body try-on modes");
    AssertEqual("cat-vton", selfHosted.Capabilities.ProviderMode, "self-hosted CatVTON should expose its provider mode");
    AssertEqual("model-x:cat-vton", selfHosted.Capabilities.SettingsHash, "self-hosted CatVTON settings hash should include explicit provider mode");

    AssertSupportedModes(
        imageEdit,
        new[] { TryOnMode.ExperimentalCompositeTryOn },
        "general image edit should only advertise composite mode");
    AssertEqual("image-edit", imageEdit.Capabilities.ProviderMode, "general image edit should expose its provider mode");
    AssertEqual("model-x:image-edit", imageEdit.Capabilities.SettingsHash, "general image edit settings hash should include explicit provider mode");
}

static void TestJsonProviderRejectsUnsupportedModesBeforeNetworkCall()
{
    var handler = new RecordingHttpProviderHandler();
    var provider = new CompositeFashnTryOnProvider(
        new HttpClient(handler),
        new HttpTryOnProviderSettings("https://api.test/v1/", "try-on", "test-key", "composite-v1", RequiresApiKey: true));

    AssertThrows<InvalidOperationException>(
        () => provider.Generate(CreateProviderRequest(CreateSingleGarmentOutfit(), TryOnMode.SingleGarmentTryOn)),
        "json provider should reject unsupported modes");
    AssertEqual(0, handler.Requests.Count, "unsupported mode must stop before network call");
}

static void TestFashnProviderSendsOnlyBodyTryOnItems()
{
    var handler = new RecordingFashnHandler();
    handler.EnqueueJson(HttpStatusCode.OK, "{\"id\":\"prediction-top\",\"error\":null}");
    handler.EnqueueJson(HttpStatusCode.OK, "{\"id\":\"prediction-top\",\"status\":\"completed\",\"output\":[\"https://cdn.fashn.ai/top.png\"],\"error\":null}");
    handler.EnqueueJson(HttpStatusCode.OK, "{\"id\":\"prediction-bottom\",\"error\":null}");
    handler.EnqueueJson(HttpStatusCode.OK, "{\"id\":\"prediction-bottom\",\"status\":\"completed\",\"output\":[\"https://cdn.fashn.ai/final.png\"],\"error\":null}");
    var outfit = CreateOutfitWithItems(
        new OutfitItem(Guid.NewGuid(), "white tee", GarmentCategory.Top, BodyZone.Torso, "https://app.test/shirt.png"),
        new OutfitItem(Guid.NewGuid(), "jeans", GarmentCategory.Bottom, BodyZone.Legs, "https://app.test/jeans.png"),
        new OutfitItem(Guid.NewGuid(), "bag", GarmentCategory.Bag, BodyZone.Accessory, "https://app.test/bag.png"));
    var bodyItems = outfit.Items.Where(item => item.Category is GarmentCategory.Top or GarmentCategory.Bottom).ToArray();
    var visualItems = outfit.Items.Where(item => item.Category == GarmentCategory.Bag).ToArray();
    var provider = new FashnTryOnProvider(
        new HttpClient(handler) { BaseAddress = new Uri("https://api.test/v1/") },
        CreateTestFashnSettings("test-key", "tryon-v1.6", "balanced", 2));

    var generation = provider.Generate(new TryOnProviderRequest(
        "user-a",
        outfit.Id,
        TryOnMode.SequentialOutfitTryOn,
        "https://app.test/user.jpg",
        bodyItems,
        visualItems,
        new TryOnGenerationSettings("tryon-v1.6", "balanced", "settings-a")));

    AssertEqual("https://cdn.fashn.ai/final.png", generation.OutputImageUrl, "fashn should return the final normal-mode output.");
    AssertEqual(4, handler.Requests.Count, "fashn should run once per body try-on item.");
    AssertTrue(!handler.Requests.Any(request => request.Body.Contains("bag.png", StringComparison.Ordinal)), "normal FASHN runs must not send visual-only items.");
}

static void TestFashnProviderRequiresApiKey()
{
    var handler = new RecordingFashnHandler();
    var provider = new FashnTryOnProvider(
        new HttpClient(handler) { BaseAddress = new Uri("https://api.test/v1/") },
        CreateTestFashnSettings("", "tryon-v1.6", "balanced", 1));

    AssertThrows<InvalidOperationException>(
        () => provider.Generate(CreateProviderRequest(CreateSingleGarmentOutfit(), TryOnMode.SingleGarmentTryOn)),
        "fashn provider should require an API key");
    AssertEqual(0, handler.Requests.Count, "missing key must stop before network call");
}

static void TestFashnProviderSendsConfiguredGenerationOptions()
{
    var handler = new RecordingFashnHandler();
    handler.EnqueueJson(HttpStatusCode.OK, "{\"id\":\"prediction-1\",\"error\":null}");
    handler.EnqueueJson(HttpStatusCode.OK, "{\"id\":\"prediction-1\",\"status\":\"completed\",\"output\":[\"https://cdn.fashn.ai/output.webp\"],\"error\":null}");
    var provider = new FashnTryOnProvider(
        new HttpClient(handler) { BaseAddress = new Uri("https://api.test/v1/") },
        new FashnTryOnSettings(
            "test-key",
            "tryon-v1.6",
            "quality",
            2,
            TimeSpan.Zero,
            NumSamples: 2,
            OutputFormat: "webp",
            ReturnBase64: true,
            SegmentationFree: false,
            GarmentPhotoType: "model",
            Seed: 42));

    provider.Generate(CreateProviderRequest(CreateSingleGarmentOutfit(), TryOnMode.SingleGarmentTryOn));

    using var body = JsonDocument.Parse(handler.Requests[0].Body);
    var inputs = body.RootElement.GetProperty("inputs");
    AssertEqual("quality", inputs.GetProperty("mode").GetString(), "request should use configured FASHN mode.");
    AssertEqual(2, inputs.GetProperty("num_samples").GetInt32(), "request should use configured sample count.");
    AssertEqual("webp", inputs.GetProperty("output_format").GetString(), "request should use configured output format.");
    AssertTrue(inputs.GetProperty("return_base64").GetBoolean(), "request should use configured base64 return flag.");
    AssertTrue(!inputs.GetProperty("segmentation_free").GetBoolean(), "request should use configured segmentation mode.");
    AssertEqual("model", inputs.GetProperty("garment_photo_type").GetString(), "request should use configured garment photo type.");
    AssertEqual(42, inputs.GetProperty("seed").GetInt32(), "request should use configured seed.");
    AssertTrue(!inputs.TryGetProperty("person_hint", out _), "legacy FASHN requests should not send stale person hint configuration.");
}

static void TestFashnDefaultResolutionChargesBaseCredits()
{
    var settings = new FashnTryOnSettings(
        "test-key",
        "tryon-max",
        "quality",
        2,
        TimeSpan.Zero,
        NumSamples: 1,
        OutputFormat: "png",
        ReturnBase64: false,
        SegmentationFree: true,
        GarmentPhotoType: "auto",
        Seed: 42);

    AssertEqual("1k", settings.Resolution, "FASHN should default to 1k resolution per the documented default.");
    AssertEqual(2, settings.CreditsPerRun, "tryon-max quality at the default 1k resolution should charge the base credit amount, not the 4k premium.");
}

static void TestFashnProviderSendsTryOnMaxQualityGenderPrompt()
{
    var handler = new RecordingFashnHandler();
    handler.EnqueueJson(HttpStatusCode.OK, "{\"id\":\"prediction-1\",\"error\":null}");
    handler.EnqueueJson(HttpStatusCode.OK, "{\"id\":\"prediction-1\",\"status\":\"completed\",\"output\":[\"https://cdn.fashn.ai/output.png\"],\"error\":null}");
    var provider = new FashnTryOnProvider(
        new HttpClient(handler) { BaseAddress = new Uri("https://api.test/v1/") },
        new FashnTryOnSettings(
            "test-key",
            "tryon-max",
            "quality",
            2,
            TimeSpan.Zero,
            NumSamples: 1,
            OutputFormat: "png",
            ReturnBase64: false,
            SegmentationFree: true,
            GarmentPhotoType: "auto",
            Seed: 42,
            Resolution: "4k",
            GenderPromptTemplate: "Use a {gender} model for this virtual try-on."));

    var request = CreateProviderRequest(CreateSingleGarmentOutfit(), TryOnMode.SingleGarmentTryOn) with
    {
        UserGender = UserGender.Female
    };
    provider.Generate(request);

    AssertEqual("tryon-max", provider.Capabilities.ModelName, "FASHN should use the best model by configuration.");
    AssertEqual(5, provider.Capabilities.CreditsPerRun, "tryon-max quality 4k should charge the premium FASHN credit amount.");
    using var body = JsonDocument.Parse(handler.Requests[0].Body);
    AssertEqual("tryon-max", body.RootElement.GetProperty("model_name").GetString(), "request should send tryon-max model.");
    var inputs = body.RootElement.GetProperty("inputs");
    AssertEqual("https://app.test/user.jpg", inputs.GetProperty("model_image").GetString(), "tryon-max should send model image.");
    AssertEqual("https://app.test/shirt.png", inputs.GetProperty("product_image").GetString(), "tryon-max should send product image.");
    AssertEqual("quality", inputs.GetProperty("generation_mode").GetString(), "tryon-max should use quality generation.");
    AssertEqual("4k", inputs.GetProperty("resolution").GetString(), "tryon-max should request 4k output.");
    AssertEqual(1, inputs.GetProperty("num_images").GetInt32(), "tryon-max should request one image.");
    AssertEqual("png", inputs.GetProperty("output_format").GetString(), "tryon-max should use configured output format.");
    AssertTrue(inputs.GetProperty("prompt").GetString()?.Contains("female", StringComparison.OrdinalIgnoreCase) == true, "tryon-max prompt should include user gender.");
}

static void TestFashnProviderOmitsPromptWhenNoTemplateConfigured()
{
    var handler = new RecordingFashnHandler();
    handler.EnqueueJson(HttpStatusCode.OK, "{\"id\":\"prediction-1\",\"error\":null}");
    handler.EnqueueJson(HttpStatusCode.OK, "{\"id\":\"prediction-1\",\"status\":\"completed\",\"output\":[\"https://cdn.fashn.ai/output.png\"],\"error\":null}");
    var provider = new FashnTryOnProvider(
        new HttpClient(handler) { BaseAddress = new Uri("https://api.test/v1/") },
        new FashnTryOnSettings(
            "test-key",
            "tryon-max",
            "quality",
            2,
            TimeSpan.Zero,
            NumSamples: 1,
            OutputFormat: "png",
            ReturnBase64: false,
            SegmentationFree: true,
            GarmentPhotoType: "auto",
            Seed: 42,
            Resolution: "4k",
            GenderPromptTemplate: ""));

    var request = CreateProviderRequest(CreateSingleGarmentOutfit(), TryOnMode.SingleGarmentTryOn) with
    {
        UserGender = UserGender.Female
    };
    provider.Generate(request);

    using var body = JsonDocument.Parse(handler.Requests[0].Body);
    var inputs = body.RootElement.GetProperty("inputs");
    AssertTrue(!inputs.TryGetProperty("prompt", out _), "tryon-max must not send a prompt when no gender prompt template is configured.");
}

static void TestFashnProviderSubmitsRequestAndPollsStatus()
{
    var handler = new RecordingFashnHandler();
    handler.EnqueueJson(HttpStatusCode.OK, "{\"id\":\"prediction-1\",\"error\":null}");
    handler.EnqueueJson(HttpStatusCode.OK, "{\"id\":\"prediction-1\",\"status\":\"completed\",\"output\":[\"https://cdn.fashn.ai/output.png\"],\"error\":null}");
    var provider = new FashnTryOnProvider(
        new HttpClient(handler) { BaseAddress = new Uri("https://api.test/v1/") },
        CreateTestFashnSettings("test-key", "tryon-v1.6", "performance", 2));

    var generation = provider.Generate(CreateProviderRequest(CreateSingleGarmentOutfit(), TryOnMode.SingleGarmentTryOn));

    AssertEqual("prediction-1", generation.ProviderJobId, "provider job id should come from FASHN");
    AssertEqual("https://cdn.fashn.ai/output.png", generation.OutputImageUrl, "output url should come from completed status");
    AssertEqual(2, handler.Requests.Count, "provider should submit and poll once");
    AssertEqual("Bearer", handler.Requests[0].Authorization?.Scheme, "request should use bearer auth");
    AssertEqual("test-key", handler.Requests[0].Authorization?.Parameter, "request should use configured API key");

    using var body = JsonDocument.Parse(handler.Requests[0].Body);
    AssertEqual("tryon-v1.6", body.RootElement.GetProperty("model_name").GetString(), "request should use configured model");
    var inputs = body.RootElement.GetProperty("inputs");
    AssertEqual("https://app.test/user.jpg", inputs.GetProperty("model_image").GetString(), "request should send body reference photo");
    AssertEqual("https://app.test/shirt.png", inputs.GetProperty("garment_image").GetString(), "request should send garment image");
    AssertEqual("tops", inputs.GetProperty("category").GetString(), "top garments should map to FASHN tops category");
    AssertEqual("performance", inputs.GetProperty("mode").GetString(), "request should use configured mode");
}

static void TestFashnProviderRejectsMultiGarmentOutfitsWhenSequentialOff()
{
    var handler = new RecordingFashnHandler();
    var provider = new FashnTryOnProvider(
        new HttpClient(handler) { BaseAddress = new Uri("https://api.test/v1/") },
        CreateTestFashnSettings("test-key", "tryon-v1.6", "balanced", 1));

    AssertThrows<InvalidOperationException>(
        () => provider.Generate(CreateProviderRequest(CreateTwoGarmentOutfit(), TryOnMode.SingleGarmentTryOn)),
        "fashn provider should fail clearly for multi-garment outfits when sequential flow is off");
    AssertEqual(0, handler.Requests.Count, "unsupported outfit shape must stop before network call");
}

static void TestFashnProviderRunsSequentialMultiGarmentOutfits()
{
    var handler = new RecordingFashnHandler();
    handler.EnqueueJson(HttpStatusCode.OK, "{\"id\":\"prediction-top\",\"error\":null}");
    handler.EnqueueJson(HttpStatusCode.OK, "{\"id\":\"prediction-top\",\"status\":\"completed\",\"output\":[\"https://cdn.fashn.ai/top.png\"],\"error\":null}");
    handler.EnqueueJson(HttpStatusCode.OK, "{\"id\":\"prediction-bottom\",\"error\":null}");
    handler.EnqueueJson(HttpStatusCode.OK, "{\"id\":\"prediction-bottom\",\"status\":\"completed\",\"output\":[\"https://cdn.fashn.ai/final.png\"],\"error\":null}");
    var provider = new FashnTryOnProvider(
        new HttpClient(handler) { BaseAddress = new Uri("https://api.test/v1/") },
        CreateTestFashnSettings("test-key", "tryon-v1.6", "balanced", 2));

    var generation = provider.Generate(CreateProviderRequest(CreateTwoGarmentOutfit(), TryOnMode.SequentialOutfitTryOn));

    AssertEqual("prediction-bottom", generation.ProviderJobId, "sequential flow should expose the final provider job id");
    AssertEqual("https://cdn.fashn.ai/final.png", generation.OutputImageUrl, "sequential flow should return the final generated image");
    AssertEqual(4, handler.Requests.Count, "sequential flow should submit and poll once per garment");

    using var firstRun = JsonDocument.Parse(handler.Requests[0].Body);
    AssertEqual("https://app.test/user.jpg", firstRun.RootElement.GetProperty("inputs").GetProperty("model_image").GetString(), "first run should use the body reference photo");
    AssertEqual("https://app.test/shirt.png", firstRun.RootElement.GetProperty("inputs").GetProperty("garment_image").GetString(), "first run should use the top garment");

    using var secondRun = JsonDocument.Parse(handler.Requests[2].Body);
    AssertEqual("https://cdn.fashn.ai/top.png", secondRun.RootElement.GetProperty("inputs").GetProperty("model_image").GetString(), "second run should use the first output as model image");
    AssertEqual("https://app.test/jeans.png", secondRun.RootElement.GetProperty("inputs").GetProperty("garment_image").GetString(), "second run should use the bottom garment");
}

static CreateGarmentCommand CreateGarment(string userId, string name, GarmentCategory category)
{
    return new CreateGarmentCommand(
        userId,
        name,
        category,
        $"https://example.com/{Uri.EscapeDataString(name)}.jpg",
        null,
        Array.Empty<string>());
}

#pragma warning disable CS8321
static FashnTryOnSettings CreateFashnSettings(IConfiguration configuration)
{
    var section = configuration.GetSection("Fashn");

    return new FashnTryOnSettings(
        ApiKey: section["ApiKey"] ?? "",
        ModelName: section["ModelName"] ?? "tryon-v1.6",
        Mode: section["Mode"] ?? "balanced",
        MaxPollingAttempts: ReadInt(section["MaxPollingAttempts"], 90),
        PollInterval: TimeSpan.FromSeconds(ReadInt(section["PollIntervalSeconds"], 2)),

        NumSamples: ReadInt(section["NumSamples"], 1),
        OutputFormat: section["OutputFormat"] ?? "png",
        ReturnBase64: ReadBool(section["ReturnBase64"], false),

        // Для твоей проблемы я бы начал именно с false.
        SegmentationFree: ReadBool(section["SegmentationFree"], false),

        GarmentPhotoType: section["GarmentPhotoType"] ?? "auto",
        Seed: ReadNullableInt(section["Seed"]));
}
#pragma warning restore CS8321

static FashnTryOnSettings CreateTestFashnSettings(string apiKey, string modelName, string mode, int maxPollingAttempts)
{
    return new FashnTryOnSettings(
        apiKey,
        modelName,
        mode,
        maxPollingAttempts,
        TimeSpan.Zero,
        NumSamples: 1,
        OutputFormat: "png",
        ReturnBase64: false,
        SegmentationFree: true,
        GarmentPhotoType: "auto",
        Seed: null);
}

static int ReadInt(string? value, int fallback)
{
    return int.TryParse(value, out var result) ? result : fallback;
}

static bool ReadBool(string? value, bool fallback)
{
    return bool.TryParse(value, out var result) ? result : fallback;
}

static int? ReadNullableInt(string? value)
{
    return int.TryParse(value, out var result) ? result : null;
}

static byte[] MinimalPngBytes()
{
    return Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAANSURBVBhXY/j///9/AAn7A/0FQ0XKAAAAAElFTkSuQmCC");
}

// An opaque rectangle centered on a transparent canvas — a garment silhouette whose alpha
// bounding box is exactly rectWidth x rectHeight regardless of the canvas (padding) size.
static byte[] TransparentPaddedRectanglePng(int width, int height, int rectWidth, int rectHeight)
{
    using var image = new Image<Rgba32>(width, height);
    var startX = (width - rectWidth) / 2;
    var startY = (height - rectHeight) / 2;
    image.ProcessPixelRows(accessor =>
    {
        for (var y = startY; y < startY + rectHeight; y++)
        {
            var row = accessor.GetRowSpan(y);
            for (var x = startX; x < startX + rectWidth; x++)
            {
                row[x] = new Rgba32(30, 30, 35, 255);
            }
        }
    });

    using var output = new MemoryStream();
    image.Save(output, new SixLabors.ImageSharp.Formats.Png.PngEncoder());
    return output.ToArray();
}

// An opaque garment rectangle on a transparent canvas, plus scattered 2x2 opaque specks in the
// corners and mid-edges — simulating leftover background pixels an imperfect keyer misses.
static byte[] GarmentWithScatteredSpecksPng(int width, int height, int rectWidth, int rectHeight)
{
    using var image = new Image<Rgba32>(width, height);
    var startX = (width - rectWidth) / 2;
    var startY = (height - rectHeight) / 2;
    var garment = new Rgba32(30, 30, 35, 255);
    var speck = new Rgba32(40, 44, 60, 255);
    var speckCentres = new[]
    {
        (X: 1, Y: 1), (X: width - 2, Y: 1), (X: 1, Y: height - 2), (X: width - 2, Y: height - 2),
        (X: width / 2, Y: 2), (X: 2, Y: height / 2), (X: width - 3, Y: height / 2)
    };
    image.ProcessPixelRows(accessor =>
    {
        for (var y = startY; y < startY + rectHeight; y++)
        {
            var row = accessor.GetRowSpan(y);
            for (var x = startX; x < startX + rectWidth; x++)
            {
                row[x] = garment;
            }
        }

        foreach (var (cx, cy) in speckCentres)
        {
            for (var dy = 0; dy <= 1; dy++)
            {
                var yy = cy + dy;
                if (yy < 0 || yy >= accessor.Height) continue;
                var row = accessor.GetRowSpan(yy);
                for (var dx = 0; dx <= 1; dx++)
                {
                    var xx = cx + dx;
                    if (xx >= 0 && xx < row.Length) row[xx] = speck;
                }
            }
        }
    });

    using var output = new MemoryStream();
    image.Save(output, new SixLabors.ImageSharp.Formats.Png.PngEncoder());
    return output.ToArray();
}

static byte[] TiltedRectanglePng(int width, int height, int rectWidth, int rectHeight, double tiltDegrees)
{
    using var image = new Image<Rgba32>(width, height);
    var startX = (width - rectWidth) / 2;
    var startY = (height - rectHeight) / 2;
    image.ProcessPixelRows(accessor =>
    {
        for (var y = startY; y < startY + rectHeight; y++)
        {
            var row = accessor.GetRowSpan(y);
            for (var x = startX; x < startX + rectWidth; x++)
            {
                row[x] = new Rgba32(30, 30, 35, 255);
            }
        }
    });

    if (Math.Abs(tiltDegrees) > 0.01)
    {
        image.Mutate(operation => operation.Rotate((float)tiltDegrees));
    }

    using var output = new MemoryStream();
    image.Save(output, new SixLabors.ImageSharp.Formats.Png.PngEncoder());
    return output.ToArray();
}

static bool ImageHasTransparentPixel(string path)
{
    using var image = Image.Load<Rgba32>(path);
    var hasTransparentPixel = false;
    image.ProcessPixelRows(accessor =>
    {
        for (var y = 0; y < accessor.Height && !hasTransparentPixel; y++)
        {
            var row = accessor.GetRowSpan(y);
            for (var x = 0; x < row.Length; x++)
            {
                if (row[x].A < 255)
                {
                    hasTransparentPixel = true;
                    break;
                }
            }
        }
    });

    return hasTransparentPixel;
}

static void PutTestObject(IObjectStorage objects, string objectKey)
{
    var bytes = MinimalPngBytes();
    using var stream = new MemoryStream(bytes);
    objects.PutObject(new ObjectStoragePutRequest(objectKey, "image/png", stream, Private: true));
}

static Outfit CreateSingleGarmentOutfit()
{
    return new Outfit(
        Guid.NewGuid(),
        "user-a",
        "white tee",
        new[]
        {
            new OutfitItem(Guid.NewGuid(), "white tee", GarmentCategory.Top, BodyZone.Torso, "https://app.test/shirt.png")
        },
        Array.Empty<string>(),
        Array.Empty<string>(),
        false,
        false,
        null,
        null,
        DateTimeOffset.UtcNow);
}

static Outfit CreateTwoGarmentOutfit()
{
    return new Outfit(
        Guid.NewGuid(),
        "user-a",
        "full outfit",
        new[]
        {
            new OutfitItem(Guid.NewGuid(), "white tee", GarmentCategory.Top, BodyZone.Torso, "https://app.test/shirt.png"),
            new OutfitItem(Guid.NewGuid(), "jeans", GarmentCategory.Bottom, BodyZone.Legs, "https://app.test/jeans.png")
        },
        Array.Empty<string>(),
        Array.Empty<string>(),
        false,
        false,
        null,
        null,
        DateTimeOffset.UtcNow);
}

static TryOnProviderRequest CreateProviderRequest(Outfit outfit, TryOnMode mode)
{
    var bodyItems = outfit.Items
        .Where(item => item.Category is GarmentCategory.Top or GarmentCategory.Bottom or GarmentCategory.Dress or GarmentCategory.Outerwear)
        .ToArray();
    var visualItems = outfit.Items
        .Where(item => item.Category is GarmentCategory.Shoes or GarmentCategory.Bag or GarmentCategory.Accessory)
        .ToArray();
    return new TryOnProviderRequest(
        outfit.UserId,
        outfit.Id,
        mode,
        "https://app.test/user.jpg",
        bodyItems,
        visualItems,
        new TryOnGenerationSettings("tryon-v1.6", "balanced", "tryon-v1.6:balanced"));
}

static void EnsureTestUser(IUserAccountRepository store, string userId, UserGender? gender)
{
    var now = DateTimeOffset.UtcNow;
    store.AddUser(new UserAccount(
        userId,
        $"{userId}@example.test",
        $"{userId}@example.test",
        userId,
        null,
        now,
        now,
        now)
    {
        Gender = gender
    });
}

static Outfit CreateOutfitWithItems(params OutfitItem[] items)
{
    return new Outfit(
        Guid.NewGuid(),
        "user-a",
        "test outfit",
        items,
        Array.Empty<string>(),
        Array.Empty<string>(),
        false,
        false,
        null,
        null,
        DateTimeOffset.UtcNow);
}

static void TestGarmentAutoTaggerContractsExist()
{
    AssertTrue(typeof(IGarmentAutoTagger).IsInterface, "application should expose a garment auto-tagger port.");
    AssertTrue(typeof(IGarmentCutoutFactory).IsInterface, "application should expose a garment cutout factory port.");
    AssertTrue(typeof(HttpGarmentAutoTagger).GetInterfaces().Contains(typeof(IGarmentAutoTagger)), "http adapter should implement the auto-tagger port.");
    AssertTrue(typeof(DisabledGarmentAutoTagger).GetInterfaces().Contains(typeof(IGarmentAutoTagger)), "disabled adapter should implement the auto-tagger port.");
    AssertTrue(typeof(AutoGarmentAutoTagger).GetInterfaces().Contains(typeof(IGarmentAutoTagger)), "auto adapter should implement the auto-tagger port.");
    AssertTrue(typeof(GarmentCutoutFactory).GetInterfaces().Contains(typeof(IGarmentCutoutFactory)), "cutout factory adapter should implement the factory port.");
    AssertTrue(
        typeof(GarmentAutoTagService).GetConstructors().Any(constructor => constructor.GetParameters().Any(parameter => parameter.ParameterType == typeof(IGarmentAutoTagger))),
        "auto-tag service should accept a configured tagger.");
}

static void TestHttpGarmentAutoTaggerParsesClassification()
{
    var handler = new RecordingHttpProviderHandler();
    handler.EnqueueJson(
        HttpStatusCode.OK,
        "{\"provider\":\"fashionclip\",\"category\":{\"value\":\"Dress\",\"confidence\":0.72},\"colors\":[{\"name\":\"navy\",\"hex\":\"#1f2a44\",\"confidence\":0.6}],\"seasons\":[{\"value\":\"summer\",\"confidence\":0.4}],\"tags\":[{\"value\":\"casual\",\"confidence\":0.33}]}");
    var tagger = new HttpGarmentAutoTagger(
        new HttpClient(handler),
        new HttpGarmentAutoTaggerSettings("http://127.0.0.1:7100/classify", TimeSpan.FromSeconds(10)));

    var result = tagger.Classify(new GarmentAutoTagRequest("cutout.png", "image/png", MinimalPngBytes(), new[] { "work", "favorite" }));

    AssertTrue(result.IsAvailable, "http tagger should report availability on success.");
    AssertTrue(result.Category == GarmentCategory.Dress, "http tagger should map the category label to the enum.");
    AssertTrue(Math.Abs(result.CategoryConfidence - 0.72) < 1e-6, "http tagger should carry the category confidence.");
    AssertEqual(1, result.Colors.Count, "http tagger should map colors.");
    AssertEqual("navy", result.Colors[0].Name, "http tagger should map the color name.");
    AssertEqual("#1f2a44", result.Colors[0].Hex, "http tagger should map the color hex.");
    AssertEqual(1, result.Seasons.Count, "http tagger should map seasons.");
    AssertEqual("summer", result.Seasons[0].Value, "http tagger should map the season value.");
    AssertEqual(1, result.Tags.Count, "http tagger should map tags.");
    AssertEqual("casual", result.Tags[0].Value, "http tagger should map the tag value.");
    AssertEqual("/classify", handler.Requests[0].Path, "http tagger should post to the classify endpoint.");
    AssertEqual(HttpMethod.Post, handler.Requests[0].Method, "http tagger should POST the image.");
    AssertTrue(handler.Requests[0].Body.Contains("known_tags", StringComparison.Ordinal), "http tagger should send known tags as multipart fields.");
    AssertTrue(handler.Requests[0].Body.Contains("work", StringComparison.Ordinal), "http tagger should include the user's known tag values.");
    AssertTrue(handler.Requests[0].Body.Contains("cutout.png", StringComparison.Ordinal), "http tagger should send the image file part.");
}

static void TestHttpGarmentAutoTaggerMapsUnknownCategoryToNull()
{
    var handler = new RecordingHttpProviderHandler();
    handler.EnqueueJson(
        HttpStatusCode.OK,
        "{\"provider\":\"fashionclip\",\"category\":{\"value\":\"Hat\",\"confidence\":0.9},\"colors\":[],\"seasons\":[],\"tags\":[]}");
    var tagger = new HttpGarmentAutoTagger(
        new HttpClient(handler),
        new HttpGarmentAutoTaggerSettings("http://127.0.0.1:7100/classify", TimeSpan.FromSeconds(10)));

    var result = tagger.Classify(new GarmentAutoTagRequest("cutout.png", "image/png", MinimalPngBytes(), Array.Empty<string>()));

    AssertTrue(result.Category is null, "http tagger should drop a category outside the current enum (e.g. the retired Hat).");
    AssertTrue(result.IsAvailable, "an empty-but-successful classification is still available.");
}

static void TestHttpGarmentAutoTaggerThrowsOnErrorStatus()
{
    var handler = new RecordingHttpProviderHandler();
    handler.EnqueueJson(HttpStatusCode.InternalServerError, "{\"detail\":\"boom\"}");
    var tagger = new HttpGarmentAutoTagger(
        new HttpClient(handler),
        new HttpGarmentAutoTaggerSettings("http://127.0.0.1:7100/classify", TimeSpan.FromSeconds(10)));

    AssertThrows<InvalidOperationException>(
        () => tagger.Classify(new GarmentAutoTagRequest("cutout.png", "image/png", MinimalPngBytes(), Array.Empty<string>())),
        "http tagger should surface provider errors (the Auto wrapper and service catch them).");
}

static void TestDisabledGarmentAutoTaggerReturnsEmpty()
{
    var result = new DisabledGarmentAutoTagger().Classify(new GarmentAutoTagRequest("cutout.png", "image/png", MinimalPngBytes(), Array.Empty<string>()));

    AssertFalse(result.IsAvailable, "disabled tagger should report unavailable.");
    AssertTrue(result.Category is null, "disabled tagger should suggest no category.");
    AssertEqual(0, result.Colors.Count, "disabled tagger should suggest no colors.");
    AssertEqual(0, result.Seasons.Count, "disabled tagger should suggest no seasons.");
    AssertEqual(0, result.Tags.Count, "disabled tagger should suggest no tags.");
}

static void TestAutoGarmentAutoTaggerRoutesByHealth()
{
    var suggestion = new GarmentAutoTagResult(
        GarmentCategory.Top,
        0.9,
        Array.Empty<AutoTagColorSuggestion>(),
        Array.Empty<AutoTagSuggestion>(),
        Array.Empty<AutoTagSuggestion>(),
        "recording-autotag");
    var request = new GarmentAutoTagRequest("cutout.png", "image/png", MinimalPngBytes(), Array.Empty<string>());

    var healthyTagger = new AutoGarmentAutoTagger(new RecordingGarmentAutoTagger(suggestion), new DisabledGarmentAutoTagger(), () => true);
    var unhealthyPreferred = new RecordingGarmentAutoTagger(suggestion);
    var unhealthyTagger = new AutoGarmentAutoTagger(unhealthyPreferred, new DisabledGarmentAutoTagger(), () => false);
    var throwingTagger = new AutoGarmentAutoTagger(new RecordingGarmentAutoTagger(throwOnClassify: true), new DisabledGarmentAutoTagger(), () => true);

    var healthy = healthyTagger.Classify(request);
    var unhealthy = unhealthyTagger.Classify(request);
    var recovered = throwingTagger.Classify(request);

    AssertTrue(healthy.Category == GarmentCategory.Top, "auto tagger should use the service when healthy.");
    AssertFalse(unhealthy.IsAvailable, "auto tagger should degrade to no-op when the service is unhealthy.");
    AssertEqual(0, unhealthyPreferred.Calls, "auto tagger should not call the service when it is unhealthy.");
    AssertFalse(recovered.IsAvailable, "auto tagger should degrade to no-op when the service throws mid-flight.");
}

static void TestGarmentAutoTagServiceResolvesCleanCutout()
{
    var cutoutBytes = new byte[] { 1, 2, 3 };
    var originalBytes = new byte[] { 9, 9 };
    var producedBytes = new byte[] { 4, 5, 6, 7 };
    var suggestion = new GarmentAutoTagResult(
        GarmentCategory.Dress,
        0.8,
        Array.Empty<AutoTagColorSuggestion>(),
        Array.Empty<AutoTagSuggestion>(),
        Array.Empty<AutoTagSuggestion>(),
        "recording-autotag");

    // Case A: an existing cutout is classified directly; the factory is never used.
    var taggerA = new RecordingGarmentAutoTagger(suggestion);
    var readerA = new StubGarmentImageReader(cutout: cutoutBytes, original: originalBytes);
    var factoryA = new StubGarmentCutoutFactory(producedBytes);
    var resultA = new GarmentAutoTagService(taggerA, readerA, readerA, factoryA).Classify("/api/storage/signed/garments/x.png?sig=1", new[] { "work" });
    AssertTrue(resultA.Category == GarmentCategory.Dress, "service should return the tagger suggestions when a cutout exists.");
    AssertTrue(ReferenceEquals(taggerA.LastRequest?.ImageBytes, cutoutBytes), "service should classify the existing cutout bytes.");
    AssertEqual(0, factoryA.Calls, "service should not generate a cutout when one already exists.");
    AssertEqual("work", taggerA.LastRequest?.KnownTags.Single(), "service should forward known tags to the tagger.");

    // Case B: no cutout yet -> generate one from the stored original.
    var taggerB = new RecordingGarmentAutoTagger(suggestion);
    var readerB = new StubGarmentImageReader(cutout: null, original: originalBytes);
    var factoryB = new StubGarmentCutoutFactory(producedBytes);
    var resultB = new GarmentAutoTagService(taggerB, readerB, readerB, factoryB).Classify("/x.png", Array.Empty<string>());
    AssertEqual(1, factoryB.Calls, "service should generate a cutout from the original when none exists.");
    AssertTrue(ReferenceEquals(factoryB.LastInput, originalBytes), "service should feed the stored original to the cutout factory.");
    AssertTrue(ReferenceEquals(taggerB.LastRequest?.ImageBytes, producedBytes), "service should classify the freshly generated cutout.");
    AssertTrue(resultB.IsAvailable, "service should return the tagger result on success.");

    // Case C: neither cutout nor original -> empty result, tagger never called.
    var taggerC = new RecordingGarmentAutoTagger(suggestion);
    var readerC = new StubGarmentImageReader(cutout: null, original: null);
    var resultC = new GarmentAutoTagService(taggerC, readerC, readerC, new StubGarmentCutoutFactory(null)).Classify("/x.png", Array.Empty<string>());
    AssertFalse(resultC.IsAvailable, "service should return an unavailable result when the image cannot be resolved.");
    AssertEqual(0, taggerC.Calls, "service should not call the tagger when there is no image.");

    // Case D: tagger throws -> service swallows into an empty result (never throws).
    var taggerD = new RecordingGarmentAutoTagger(throwOnClassify: true);
    var readerD = new StubGarmentImageReader(cutout: cutoutBytes, original: originalBytes);
    var resultD = new GarmentAutoTagService(taggerD, readerD, readerD, new StubGarmentCutoutFactory(producedBytes)).Classify("/x.png", Array.Empty<string>());
    AssertFalse(resultD.IsAvailable, "service should never throw; a tagger failure yields an empty result.");

    // Case E: cutout generation fails -> fall back to the raw original.
    var taggerE = new RecordingGarmentAutoTagger(suggestion);
    var readerE = new StubGarmentImageReader(cutout: null, original: originalBytes);
    new GarmentAutoTagService(taggerE, readerE, readerE, new StubGarmentCutoutFactory(null)).Classify("/x.png", Array.Empty<string>());
    AssertTrue(ReferenceEquals(taggerE.LastRequest?.ImageBytes, originalBytes), "service should fall back to the original when a cutout cannot be produced.");
}

static void TestApiWiresAutoTaggingClassifyEndpoint()
{
    var program = File.ReadAllText(Path.Combine("outfit_planner_back", "src", "OutfitPlanner.Api", "Program.cs"));

    AssertTrue(program.Contains("configuration[\"AutoTagging:Provider\"] ?? \"Auto\"", StringComparison.Ordinal), "API should default auto-tagging provider to auto.");
    AssertTrue(program.Contains("\"auto\" => new AutoGarmentAutoTagger", StringComparison.Ordinal), "API should wire the auto garment auto-tagger.");
    AssertTrue(program.Contains("/uploads/garment-photo/classify", StringComparison.Ordinal), "API should expose the garment classify endpoint.");
    AssertTrue(program.Contains("AutoTagging:HttpServer:Endpoint", StringComparison.Ordinal), "API should read the auto-tagging endpoint configuration.");
    AssertTrue(program.Contains("http://127.0.0.1:7100/classify", StringComparison.Ordinal), "API should default the auto-tagging endpoint to the local service.");
    AssertTrue(program.Contains("AddSingleton<GarmentAutoTagService>", StringComparison.Ordinal), "API should register the auto-tag service.");
}

static void AssertEqual<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{message}. Expected {expected}, got {actual}.");
    }
}

static void AssertThrows<TException>(Action action, string message)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException(message);
}

static void AssertTrue(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void AssertFalse(bool condition, string message)
{
    if (condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void AssertSupportedModes(ITryOnProvider provider, IReadOnlyCollection<TryOnMode> expectedModes, string message)
{
    var supportedModes = provider.Capabilities.SupportedModes;

    AssertEqual(expectedModes.Count, supportedModes.Count, $"{message}: supported mode count");
    foreach (var mode in expectedModes)
    {
        AssertTrue(supportedModes.Contains(mode), $"{message}: should include {mode}.");
    }

    AssertTrue(!supportedModes.Contains(TryOnMode.ClothesOnlyPreview), $"{message}: HTTP providers should not advertise clothes-only preview.");
}

static void AssertAssemblyDoesNotReference(IEnumerable<string> references, IEnumerable<string> forbidden, string message)
{
    var referenceSet = references.ToHashSet(StringComparer.Ordinal);
    var forbiddenMatches = forbidden.Where(referenceSet.Contains).ToList();

    if (forbiddenMatches.Count > 0)
    {
        throw new InvalidOperationException($"{message} Forbidden references: {string.Join(", ", forbiddenMatches)}.");
    }
}

sealed class InMemoryByteObjectStorage : IObjectStorage
{
    private readonly Dictionary<string, byte[]> _objects = new(StringComparer.Ordinal);

    public StoredObject PutObject(ObjectStoragePutRequest request)
    {
        var key = Normalize(request.ObjectKey);
        using var buffer = new MemoryStream();
        request.Content.CopyTo(buffer);
        var bytes = buffer.ToArray();
        _objects[key] = bytes;
        return new StoredObject(key, request.ContentType, bytes.Length, request.Private);
    }

    // No local file path on purpose: forces server-side reads through OpenReadObject.
    public StoredObjectFile? GetObject(string objectKey) => null;

    public Stream? OpenReadObject(string objectKey)
        => _objects.TryGetValue(Normalize(objectKey), out var bytes) ? new MemoryStream(bytes) : null;

    public bool DeleteObject(string objectKey) => _objects.Remove(Normalize(objectKey));

    public int DeletePrefix(string prefix)
    {
        var normalized = Normalize(prefix).TrimEnd('/') + "/";
        var keys = _objects.Keys.Where(key => key.StartsWith(normalized, StringComparison.Ordinal)).ToList();
        foreach (var key in keys)
        {
            _objects.Remove(key);
        }

        return keys.Count;
    }

    public string CreateSignedReadUrl(string objectKey, TimeSpan lifetime)
        => $"/api/storage/signed/{Normalize(objectKey)}?expires=1&signature=test";

    private static string Normalize(string objectKey) => objectKey.Trim().Replace('\\', '/').TrimStart('/');
}

sealed class RecordingHttpProviderHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses = new();

    public List<RecordedHttpProviderRequest> Requests { get; } = new();

    public void EnqueueJson(HttpStatusCode statusCode, string body)
    {
        _responses.Enqueue(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        });
    }

    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RecordRequest(request, cancellationToken);
        return NextResponse();
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(await RecordedRequestAsync(request, cancellationToken));
        return NextResponse();
    }

    private void RecordRequest(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(RecordedRequestAsync(request, cancellationToken).GetAwaiter().GetResult());
    }

    private async Task<RecordedHttpProviderRequest> RecordedRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return new RecordedHttpProviderRequest(
            request.Method,
            request.RequestUri?.AbsolutePath ?? "",
            request.Headers.Authorization,
            request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));
    }

    private HttpResponseMessage NextResponse()
    {
        return _responses.Count > 0
            ? _responses.Dequeue()
            : new HttpResponseMessage(HttpStatusCode.InternalServerError);
    }
}

sealed record RecordedHttpProviderRequest(HttpMethod Method, string Path, AuthenticationHeaderValue? Authorization, string Body);

sealed class RecordingGarmentAutoTagger : IGarmentAutoTagger
{
    private readonly GarmentAutoTagResult? _result;
    private readonly bool _throw;

    public RecordingGarmentAutoTagger(GarmentAutoTagResult? result = null, bool throwOnClassify = false)
    {
        _result = result;
        _throw = throwOnClassify;
    }

    public string Name => "recording-autotag";

    public int Calls { get; private set; }

    public GarmentAutoTagRequest? LastRequest { get; private set; }

    public GarmentAutoTagResult Classify(GarmentAutoTagRequest request)
    {
        Calls++;
        LastRequest = request;
        if (_throw)
        {
            throw new InvalidOperationException("tagger failure");
        }

        return _result ?? GarmentAutoTagResult.Empty(Name);
    }
}

sealed class StubGarmentImageReader : IGarmentCutoutImageReader, IGarmentOriginalImageReader
{
    private readonly byte[]? _cutout;
    private readonly byte[]? _original;

    public StubGarmentImageReader(byte[]? cutout, byte[]? original)
    {
        _cutout = cutout;
        _original = original;
    }

    public byte[]? ReadGarmentCutoutImageBytes(string garmentImageUrl) => _cutout;

    public byte[]? ReadGarmentOriginalImageBytes(string garmentImageUrl) => _original;
}

sealed class StubGarmentCutoutFactory : IGarmentCutoutFactory
{
    private readonly byte[]? _cutout;

    public StubGarmentCutoutFactory(byte[]? cutout)
    {
        _cutout = cutout;
    }

    public int Calls { get; private set; }

    public byte[]? LastInput { get; private set; }

    public byte[]? CreateCutout(byte[] originalImageBytes)
    {
        Calls++;
        LastInput = originalImageBytes;
        return _cutout;
    }
}

sealed class CountingTryOnProvider : ITryOnProvider
{
    private readonly HashSet<TryOnMode> _supportedModes;

    public CountingTryOnProvider(params TryOnMode[] supportedModes)
    {
        _supportedModes = supportedModes.Length == 0
            ? new HashSet<TryOnMode>
            {
                TryOnMode.SingleGarmentTryOn,
                TryOnMode.SequentialOutfitTryOn,
                TryOnMode.ExperimentalCompositeTryOn
            }
            : supportedModes.ToHashSet();
    }

    public int Calls { get; private set; }
    public TryOnProviderRequest? LastRequest { get; private set; }
    public string Name => "test";

    public TryOnProviderCapabilities Capabilities => new(
        Name,
        "test-model",
        "test-mode",
        "test-model:test-mode",
        new HashSet<TryOnMode>(_supportedModes));

    public TryOnGeneration Generate(TryOnProviderRequest request)
    {
        Calls++;
        LastRequest = request;
        return new TryOnGeneration("test-provider-job", "https://example.com/output.jpg");
    }
}

sealed class StubGarmentUrlRefresher : IStoredPhotoUrlRefresher
{
    public string RefreshGarmentImageUrl(string photoUrl) => "https://cdn.test/cutout.png";
    public string RefreshGarmentThumbnailUrl(string photoUrl) => "https://cdn.test/thumb.png";
    public string RefreshBodyReferencePhotoUrl(string photoUrl) => photoUrl;
    public string RefreshAvatarUrl(string photoUrl) => photoUrl;
}

sealed class RecordingTryOnOutputStorage : ITryOnOutputStorage
{
    public RecordingTryOnOutputStorage(string storedUrl)
    {
        StoredUrl = storedUrl;
    }

    public string StoredUrl { get; }
    public string? LastSourceImageUrl { get; private set; }

    public Task<string> StoreAsync(Guid jobId, string sourceImageUrl, DateTimeOffset retentionUntil, CancellationToken cancellationToken = default)
    {
        LastSourceImageUrl = sourceImageUrl;
        return Task.FromResult(StoredUrl);
    }

    public bool DeleteOutput(string outputImageUrl)
    {
        return true;
    }
}

sealed class RecordingTryOnJobQueue : ITryOnJobQueue
{
    public List<Guid> Enqueued { get; } = new();
    public List<Guid> PriorityEnqueued { get; } = new();

    public ValueTask EnqueueAsync(Guid jobId, bool priority = false, CancellationToken cancellationToken = default)
    {
        if (priority)
        {
            PriorityEnqueued.Add(jobId);
        }

        Enqueued.Add(jobId);
        return ValueTask.CompletedTask;
    }

    public Task<Guid> DequeueAsync(CancellationToken cancellationToken)
    {
        if (Enqueued.Count == 0)
        {
            throw new InvalidOperationException("No queued job.");
        }

        var jobId = Enqueued[0];
        Enqueued.RemoveAt(0);
        return Task.FromResult(jobId);
    }
}

sealed class FakeBillingProvider : IBillingProvider
{
    public string Name => "fake";
    public bool Enabled { get; set; } = true;
    public BillingWebhookEvent? NextEvent { get; set; }

    public Task<string> CreateSubscriptionCheckoutAsync(UserAccount user, string priceId, string successUrl, string cancelUrl, CancellationToken cancellationToken)
    {
        return Task.FromResult("https://billing.example/checkout");
    }

    public Task<string> CreateTopUpCheckoutAsync(UserAccount user, BillingTopUpPack pack, string successUrl, string cancelUrl, CancellationToken cancellationToken)
    {
        return Task.FromResult($"https://billing.example/topup/{pack.Id}");
    }

    public Task<string> CreatePortalSessionAsync(string customerId, string returnUrl, CancellationToken cancellationToken)
    {
        return Task.FromResult($"https://billing.example/portal/{customerId}");
    }

    public BillingWebhookEvent? ParseWebhookEvent(string payload, string? signatureHeader)
    {
        return signatureHeader == "valid" ? NextEvent : null;
    }
}

sealed class ThrowingTryOnProvider : ITryOnProvider
{
    public string Name => "throwing-test";

    public TryOnGeneration Generate(TryOnProviderRequest request)
    {
        throw new InvalidOperationException("provider down");
    }
}

sealed class RecordingBackgroundRemovalProvider : IBackgroundRemovalProvider
{
    private readonly byte[] _resultBytes;

    public RecordingBackgroundRemovalProvider(byte[] resultBytes)
    {
        _resultBytes = resultBytes;
    }

    public int Calls { get; private set; }

    public BackgroundRemovalRequest? LastRequest { get; private set; }

    public string ProviderName { get; init; } = "recording";

    public string Name => ProviderName;

    public BackgroundRemovalResult RemoveBackground(BackgroundRemovalRequest request)
    {
        Calls++;
        LastRequest = request;
        return new BackgroundRemovalResult(_resultBytes, "image/png", Name);
    }
}

sealed class RecordingGarmentImageRotator : IGarmentImageRotator
{
    public double AutoStraightenAngle { get; init; } = 12d;

    public GarmentCutoutMeasurement? RotationMeasurement { get; init; }

    public int ComputeCalls { get; private set; }

    public int RotateCalls { get; private set; }

    public double? LastRotateDegrees { get; private set; }

    public double ComputeGarmentAutoStraightenAngle(string garmentImageUrl)
    {
        ComputeCalls++;
        return AutoStraightenAngle;
    }

    public GarmentRotationOutcome RotateGarment(string garmentImageUrl, double degrees)
    {
        RotateCalls++;
        LastRotateDegrees = degrees;
        return new GarmentRotationOutcome($"{garmentImageUrl}#cutout{degrees:0.##}", $"{garmentImageUrl}#thumb{degrees:0.##}", "rotated-hash", RotationMeasurement);
    }
}

sealed class RecordingBackgroundRemovalHandler : HttpMessageHandler
{
    private readonly byte[] _responseBytes;

    public RecordingBackgroundRemovalHandler(byte[] responseBytes)
    {
        _responseBytes = responseBytes;
    }

    public HttpRequestMessage? Request { get; private set; }

    public string Body { get; private set; } = "";

    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Request = request;
        Body = request.Content is null ? "" : request.Content.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();
        return ImageResponse();
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Request = request;
        Body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        return ImageResponse();
    }

    private HttpResponseMessage ImageResponse()
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(_responseBytes)
            {
                Headers =
                {
                    ContentType = new MediaTypeHeaderValue("image/png")
                }
            }
        };
    }
}

sealed class StubHairstylePresetCatalog : IHairstylePresetCatalog
{
    private readonly IReadOnlyList<HairstylePreset> _presets;

    public StubHairstylePresetCatalog(params HairstylePreset[] presets)
    {
        _presets = presets;
    }

    public IReadOnlyList<HairstylePreset> ListHairstylePresets(UserGender gender)
    {
        return _presets.Where(preset => preset.Gender == gender).ToList();
    }

    public HairstylePreset? FindHairstylePreset(string presetId)
    {
        return _presets.FirstOrDefault(preset => string.Equals(preset.Id, presetId, StringComparison.OrdinalIgnoreCase));
    }

    public StoredPhotoFile? GetHairstyleAssetFile(string assetFileName)
    {
        return null;
    }
}

sealed class TestShareTokenGenerator : IShareTokenGenerator
{
    public string CreateToken()
    {
        return "test-share-token";
    }
}

sealed class CountingPhotoStorage : IPhotoStorage
{
    public int Calls { get; private set; }

    public StoredPhoto SaveGarmentPhoto(IncomingPhoto photo)
    {
        Calls++;
        return new StoredPhoto("test.jpg", photo.ContentType, photo.Length, "/uploads/garments/test.jpg");
    }

    public StoredPhoto SaveGarmentOriginal(IncomingPhoto photo)
    {
        Calls++;
        return new StoredPhoto("test.jpg", photo.ContentType, photo.Length, "/uploads/garments/test.jpg");
    }

    public StoredPhoto SaveBodyReferencePhoto(IncomingPhoto photo)
    {
        Calls++;
        return new StoredPhoto("test.jpg", photo.ContentType, photo.Length, "/uploads/body-reference-photos/test.jpg");
    }

    public StoredPhoto SaveAvatarPhoto(IncomingPhoto photo)
    {
        Calls++;
        return new StoredPhoto("test.jpg", photo.ContentType, photo.Length, "/api/storage/signed/avatars/thumbnail/test.jpg")
        {
            ThumbnailObjectKey = "avatars/thumbnail/test.jpg"
        };
    }
}

sealed class TestPasswordHasher : IPasswordHasher
{
    public string HashPassword(string password)
    {
        return $"hashed:{password}";
    }

    public bool VerifyPassword(string passwordHash, string password)
    {
        return passwordHash == $"hashed:{password}";
    }
}

sealed class TestAuthTokenService : IAuthTokenService
{
    private int _nextToken = 1;

    public string CreateToken()
    {
        return _nextToken++ switch
        {
            1 => "session-1",
            2 => "csrf-1",
            3 => "session-2",
            4 => "csrf-2",
            5 => "session-3",
            6 => "csrf-3",
            7 => "session-4",
            8 => "csrf-4",
            _ => $"token-{_nextToken}"
        };
    }

    public string HashToken(string token)
    {
        return $"hash:{token}";
    }
}

sealed class RecordingFashnHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses = new();

    public List<RecordedFashnRequest> Requests { get; } = new();

    public void EnqueueJson(HttpStatusCode statusCode, string body)
    {
        _responses.Enqueue(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        });
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(new RecordedFashnRequest(
            request.Method,
            request.RequestUri?.AbsolutePath ?? "",
            request.Headers.Authorization,
            request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken)));

        return _responses.Count > 0
            ? _responses.Dequeue()
            : new HttpResponseMessage(HttpStatusCode.InternalServerError);
    }
}

sealed record RecordedFashnRequest(HttpMethod Method, string Path, AuthenticationHeaderValue? Authorization, string Body);
