using OutfitPlanner.Application.Abstractions;
using OutfitPlanner.Application.Services;
using OutfitPlanner.Domain;
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
    ("api uses local file store when postgres is not configured", TestApiUsesLocalFileStoreWithoutPostgres),
    ("postgres schema contains tables required by repository ports", TestPostgresSchemaContainsRepositoryTables),
    ("postgres schema contains production auth tables and indexes", TestPostgresSchemaContainsAuthTables),
    ("auth service registers email users with hashed passwords and sessions", TestAuthServiceRegistersEmailUsers),
    ("auth service requires password length digit and letter only", TestAuthServicePasswordPolicy),
    ("auth service rejects duplicate email registration", TestAuthServiceRejectsDuplicateEmailRegistration),
    ("auth service signs in existing external accounts and auto-registers missing accounts", TestAuthServiceExternalLoginAutoRegisters),
    ("auth service revokes session tokens by stored hash", TestAuthServiceRevokesSessionByHash),
    ("auth service lists sessions revokes all sessions and cleans expired sessions", TestAuthServiceSessionHardening),
    ("api exposes secure auth endpoints and cookie settings", TestApiExposesSecureAuthEndpoints),
    ("api exposes privacy and auth hardening endpoints", TestApiExposesPrivacyAndAuthHardeningEndpoints),
    ("api exposes edit delete filtering and revoke endpoints", TestApiExposesEditDeleteFilterAndRevokeEndpoints),
    ("api exposes openapi document generation", TestApiExposesOpenApiDocumentGeneration),
    ("api documents frontend response bodies for generated types", TestApiDocumentsFrontendResponseBodies),
    ("maps expanded garment categories to richer body zones", TestCategoryMapping),
    ("wardrobe service updates structured garment metadata without reupload", TestWardrobeServiceUpdatesStructuredMetadata),
    ("wardrobe service filters sorts and paginates garments", TestWardrobeServiceFiltersSortsAndPaginatesGarments),
    ("outfit service updates gets filters and deletes outfits", TestOutfitServiceUpdatesFiltersAndDeletesOutfits),
    ("outfit service applies slot compatibility rules", TestOutfitSlotCompatibilityRules),
    ("schedule service can unschedule a planned date", TestScheduleServiceUnschedulesDate),
    ("share service can revoke current user share links", TestShareServiceRevokesShareLinks),
    ("try-on estimator classifies outfit items and prices modes", TestTryOnCostEstimatorClassifiesAndPricesModes),
    ("try-on estimator marks unavailable modes", TestTryOnCostEstimatorMarksUnavailableModes),
    ("try-on service requires explicit AI consent before provider call", TestTryOnConsentRequired),
    ("try-on service estimates cost before generation", TestTryOnServiceEstimatesCost),
    ("try-on service marks provider unsupported modes unavailable", TestTryOnServiceMarksProviderUnsupportedModesUnavailable),
    ("try-on service exposes only confirmed start contract", TestTryOnServiceExposesOnlyConfirmedStartContract),
    ("try-on service enforces confirmed credits and cache key", TestTryOnServiceEnforcesConfirmedCost),
    ("try-on service returns cache hits without queueing provider work", TestTryOnServiceReturnsCacheHitsWithoutQueueing),
    ("try-on service deletes active preview output from outfit", TestTryOnServiceDeletesActivePreviewOutputFromOutfit),
    ("try-on service deletes active preview output by outfit", TestTryOnServiceDeletesActivePreviewOutputByOutfit),
    ("try-on service completes clothes-only preview without ai", TestTryOnServiceCompletesClothesOnlyWithoutAi),
    ("try-on service completes clothes-only preview without body reference", TestTryOnServiceCompletesClothesOnlyWithoutBodyReference),
    ("try-on service queues jobs without calling provider inline", TestTryOnServiceQueuesJobsWithoutInlineProviderCall),
    ("try-on processor completes queued jobs through provider", TestTryOnProcessorCompletesQueuedJobs),
    ("try-on processor sends public absolute storage urls to external providers", TestTryOnProcessorSendsPublicStorageUrlsToProvider),
    ("try-on processor stores external provider outputs before exposing them", TestTryOnProcessorStoresExternalProviderOutputs),
    ("try-on processor excludes visual-only items outside composite mode", TestTryOnProcessorExcludesVisualOnlyItemsOutsideCompositeMode),
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
    ("background removal provider contracts exist", TestBackgroundRemovalProviderContracts),
    ("background removal auto provider prefers rembg when available", TestBackgroundRemovalAutoProviderPrefersRembg),
    ("api defaults background removal provider to auto", TestApiDefaultsBackgroundRemovalToAuto),
    ("image processor delegates garment cutouts to background removal provider", TestImageProcessorDelegatesGarmentCutout),
    ("http background removal provider posts multipart image with api key", TestHttpBackgroundRemovalProviderPostsMultipartImageWithApiKey),
    ("rembg server provider posts multipart file field", TestRembgServerProviderPostsMultipartFileField),
    ("api registers rembg server provider", TestApiRegistersRembgServerProvider),
    ("single garment extraction scaffold returns one cutout", TestSingleGarmentExtractionScaffoldReturnsOneCutout),
    ("photo upload service stores garment photo variants behind signed url", TestPhotoUploadStoresGarmentPhoto),
    ("stored photo urls refresh stale garment links to cutouts", TestStoredPhotoUrlRefresherRefreshesGarmentVariants),
    ("photo upload service stores body reference photo privately", TestPhotoUploadStoresBodyReferencePhoto),
    ("wardrobe service deletes garment records and stored photos", TestWardrobeServiceDeletesGarmentAndStoredPhoto),
    ("wardrobe service deletes body reference records and stored photos", TestWardrobeServiceDeletesBodyReferenceAndStoredPhoto),
    ("postgres schema contains structured garment metadata and query indexes", TestPostgresSchemaContainsStructuredMetadataAndIndexes),
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
    AssertEqual(BodyZone.Head, GarmentRules.GetBodyZone(GarmentCategory.Hat), "Hat should map to head");
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
        typeof(IShareLinkRepository)
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

static void TestApiUsesLocalFileStoreWithoutPostgres()
{
    var program = File.ReadAllText(Path.Combine("outfit_planner_back", "src", "OutfitPlanner.Api", "Program.cs"));

    AssertTrue(program.Contains("\"LocalFile\"", StringComparison.Ordinal), "empty Postgres configuration should use a durable local file store label.");
    AssertTrue(program.Contains("FileBackedOutfitStore", StringComparison.Ordinal), "API should register the durable local file store when Postgres is not configured.");
    AssertTrue(program.Contains("CreateLocalOutfitStore", StringComparison.Ordinal), "API should centralize local file store creation.");
    AssertTrue(program.Contains("Storage:Local:DataPath", StringComparison.Ordinal), "local file store path should be configurable.");
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

static void TestAuthServiceRegistersEmailUsers()
{
    var store = new InMemoryOutfitStore();
    var auth = new AuthService(store, new TestPasswordHasher(), new TestAuthTokenService(), new SystemClock());

    var result = auth.RegisterWithPassword("Ada@Example.COM", "abc12345", "abc12345");

    AssertEqual("ada@example.com", result.User.Email, "registration should normalize email addresses.");
    AssertTrue(result.User.Id.StartsWith("usr_", StringComparison.Ordinal), "registered users should receive opaque user ids.");
    AssertTrue(!string.IsNullOrWhiteSpace(result.SessionToken), "registration should issue a session token.");
    AssertTrue(!string.IsNullOrWhiteSpace(result.CsrfToken), "registration should issue a CSRF token.");
    AssertTrue(store.GetUserByNormalizedEmail("ada@example.com")?.PasswordHash?.StartsWith("hashed:", StringComparison.Ordinal) == true, "passwords should be hashed before storage.");
    AssertTrue(store.GetActiveAuthSessionByTokenHash("hash:session-1", DateTimeOffset.UtcNow) is not null, "session lookup should use the token hash.");
}

static void TestAuthServicePasswordPolicy()
{
    var store = new InMemoryOutfitStore();
    var auth = new AuthService(store, new TestPasswordHasher(), new TestAuthTokenService(), new SystemClock());

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
    var auth = new AuthService(store, new TestPasswordHasher(), new TestAuthTokenService(), new SystemClock());

    auth.RegisterWithPassword("ada@example.com", "abc12345", "abc12345");

    AssertThrows<InvalidOperationException>(
        () => auth.RegisterWithPassword("ADA@example.com", "abc12345", "abc12345"),
        "duplicate normalized emails must be rejected");
}

static void TestAuthServiceExternalLoginAutoRegisters()
{
    var store = new InMemoryOutfitStore();
    var auth = new AuthService(store, new TestPasswordHasher(), new TestAuthTokenService(), new SystemClock());

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
    var auth = new AuthService(store, new TestPasswordHasher(), new TestAuthTokenService(), new SystemClock());
    var result = auth.RegisterWithPassword("session@example.com", "abc12345", "abc12345");

    auth.RevokeSession(result.SessionToken);

    AssertTrue(store.GetActiveAuthSessionByTokenHash("hash:session-1", DateTimeOffset.UtcNow) is null, "revoked sessions should no longer authenticate.");
}

static void TestAuthServiceSessionHardening()
{
    var store = new InMemoryOutfitStore();
    var auth = new AuthService(store, new TestPasswordHasher(), new TestAuthTokenService(), new SystemClock());
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
        new OutfitItem(Guid.Parse("10000000-0000-0000-0000-000000000007"), "scarf", GarmentCategory.Accessory, BodyZone.Accessory, "https://app.test/scarf.png"),
        new OutfitItem(Guid.Parse("10000000-0000-0000-0000-000000000008"), "hat", GarmentCategory.Hat, BodyZone.Head, "https://app.test/hat.png"));
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
    AssertEqual(4, sequential.VisualOnlyItems.Count, "sequential estimate should classify visual-only items.");
    AssertTrue(sequential.VisualOnlyItems.Any(item => item.Category == GarmentCategory.Shoes), "shoes should be a visual-only category.");
    AssertTrue(sequential.VisualOnlyItems.Any(item => item.Category == GarmentCategory.Bag), "bag should be a visual-only category.");
    AssertTrue(sequential.VisualOnlyItems.Any(item => item.Category == GarmentCategory.Accessory), "accessory should be a visual-only category.");
    AssertTrue(sequential.VisualOnlyItems.Any(item => item.Category == GarmentCategory.Hat), "hat should be a visual-only category.");
    AssertEqual(4, sequential.EstimatedCredits, "sequential estimate should cost one credit per body try-on item.");
    AssertTrue(sequential.IsAvailable, "sequential estimate should be available for multiple body items.");
    AssertTrue(sequential.RequiresAi, "sequential estimate should require AI.");
    AssertTrue(!sequential.RequiresPremiumConfirmation, "sequential estimate should not be premium.");
    AssertEqual(4, sequential.IncludedGarmentIds.Count, "sequential estimate should include only body try-on items.");
    AssertEqual(4, sequential.ExcludedGarmentIds.Count, "sequential estimate should exclude visual-only items.");
    AssertTrue(sequential.CacheKey.Length == 64, "cache key should be a SHA-256 hex string.");
    AssertTrue(sequential.CacheKey.All(character => char.IsDigit(character) || character is >= 'a' and <= 'f'), "cache key should be lowercase SHA-256 hex.");

    AssertEqual(1, composite.EstimatedCredits, "composite estimate should cost one credit.");
    AssertEqual(8, composite.IncludedGarmentIds.Count, "composite estimate should include body and visual items.");
    AssertTrue(composite.RequiresPremiumConfirmation, "composite estimate should require premium confirmation.");
    AssertTrue(composite.HasCachedResult, "estimate should carry cache hit status from the caller.");
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
        "(\"FASHN_PERSON_HINT\", \"Fashn:PersonHint\")"
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
    foreach (var variant in new[] { "Original", "Thumbnail", "ProcessedCutout", "TryOnOutput", "PrivatePreview", "SegmentationMask" })
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
        "Hat",
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
            Seed: 42,
            PersonHint: "original"));

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
    AssertEqual("original", inputs.GetProperty("person_hint").GetString(), "request should use configured person hint.");
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
        Seed: ReadNullableInt(section["Seed"]),
        PersonHint: section["PersonHint"]);
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
        Seed: null,
        PersonHint: null);
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
        .Where(item => item.Category is GarmentCategory.Shoes or GarmentCategory.Bag or GarmentCategory.Accessory or GarmentCategory.Hat)
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

    public ValueTask EnqueueAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
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

    public StoredPhoto SaveBodyReferencePhoto(IncomingPhoto photo)
    {
        Calls++;
        return new StoredPhoto("test.jpg", photo.ContentType, photo.Length, "/uploads/body-reference-photos/test.jpg");
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
