using OutfitPlanner.Application.Abstractions;
using OutfitPlanner.Application.Services;
using OutfitPlanner.Domain;
using OutfitPlanner.Infrastructure.Security;
using OutfitPlanner.Infrastructure.Storage;
using OutfitPlanner.Infrastructure.TryOn;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

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
    ("try-on service queues jobs without calling provider inline", TestTryOnServiceQueuesJobsWithoutInlineProviderCall),
    ("try-on processor completes queued jobs through provider", TestTryOnProcessorCompletesQueuedJobs),
    ("try-on service forwards sequential flow option to provider", TestTryOnServiceForwardsSequentialFlowOption),
    ("api registers redis try-on queue and provider choices", TestApiRegistersRedisQueueAndProviderChoices),
    ("schedule service stores one planned outfit per user and day", TestDailySchedulePerUser),
    ("share token generator emits url safe high entropy tokens", TestShareTokenGenerator),
    ("photo upload service rejects unsupported content types", TestPhotoUploadRejectsUnsupportedContentType),
    ("photo upload service rejects forged image content type by magic bytes", TestPhotoUploadRejectsForgedImageContentType),
    ("photo upload service accepts large phone photos", TestPhotoUploadAcceptsLargePhonePhotos),
    ("api configures upload body limits", TestApiConfiguresUploadBodyLimits),
    ("api exposes test diagnostics and trace ids", TestApiExposesTestDiagnosticsAndTraceIds),
    ("object storage ports and local/minio adapters exist", TestObjectStoragePortsAndAdapters),
    ("image processing pipeline exposes privacy preserving variants", TestImageProcessingPipelineContracts),
    ("photo upload service stores garment photo variants behind signed url", TestPhotoUploadStoresGarmentPhoto),
    ("photo upload service stores body reference photo privately", TestPhotoUploadStoresBodyReferencePhoto),
    ("wardrobe service deletes garment records and stored photos", TestWardrobeServiceDeletesGarmentAndStoredPhoto),
    ("wardrobe service deletes body reference records and stored photos", TestWardrobeServiceDeletesBodyReferenceAndStoredPhoto),
    ("postgres schema contains structured garment metadata and query indexes", TestPostgresSchemaContainsStructuredMetadataAndIndexes),
    ("postgres schema contains privacy storage auth hardening and try-on retention fields", TestPostgresSchemaContainsPrivacyStorageAuthAndRetentionFields),
    ("api uses DbUp migrations instead of startup schema initializer", TestApiUsesDbUpMigrations),
    ("new try-on provider adapters implement provider port", TestProviderAdaptersImplementPort),
    ("fashn provider requires api key before network call", TestFashnProviderRequiresApiKey),
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
        new OutfitItem(Guid.Parse("10000000-0000-0000-0000-000000000003"), "loafers", GarmentCategory.Shoes, BodyZone.Feet, "https://app.test/shoes.png"),
        new OutfitItem(Guid.Parse("10000000-0000-0000-0000-000000000004"), "bag", GarmentCategory.Bag, BodyZone.Accessory, "https://app.test/bag.png"));
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

    AssertEqual(2, sequential.BodyTryOnItems.Count, "sequential estimate should classify body try-on items.");
    AssertEqual(2, sequential.VisualOnlyItems.Count, "sequential estimate should classify visual-only items.");
    AssertEqual(2, sequential.EstimatedCredits, "sequential estimate should cost one credit per body try-on item.");
    AssertTrue(sequential.IsAvailable, "sequential estimate should be available for multiple body items.");
    AssertTrue(sequential.RequiresAi, "sequential estimate should require AI.");
    AssertTrue(!sequential.RequiresPremiumConfirmation, "sequential estimate should not be premium.");
    AssertEqual(2, sequential.IncludedGarmentIds.Count, "sequential estimate should include only body try-on items.");
    AssertEqual(2, sequential.ExcludedGarmentIds.Count, "sequential estimate should exclude visual-only items.");
    AssertTrue(sequential.CacheKey.Length == 64, "cache key should be a SHA-256 hex string.");

    AssertEqual(1, composite.EstimatedCredits, "composite estimate should cost one credit.");
    AssertEqual(4, composite.IncludedGarmentIds.Count, "composite estimate should include body and visual items.");
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
        .CreateOutfit(userId, "casual", new[] { top.Id, bottom.Id });
    var provider = new CountingTryOnProvider();
    var service = new TryOnService(store, store, new RecordingTryOnJobQueue(), provider, new SystemClock());

    AssertThrows<InvalidOperationException>(
        () => service.Start(userId, outfit.Id, "https://example.com/person.jpg", consentAccepted: false),
        "try-on should require consent");
    AssertEqual(0, provider.Calls, "provider must not receive photos without consent");
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
    var service = new TryOnService(store, store, queue, provider, new SystemClock());

    var job = service.StartAsync(userId, outfit.Id, "https://example.com/person.jpg", consentAccepted: true)
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
    var service = new TryOnService(store, store, queue, provider, new SystemClock());
    var job = service.StartAsync(userId, outfit.Id, "https://example.com/person.jpg", consentAccepted: true, sequentialFlowEnabled: true)
        .GetAwaiter()
        .GetResult();

    service.ProcessQueuedJobAsync(job.Id).GetAwaiter().GetResult();
    var completed = service.GetJob(userId, job.Id);
    var updatedOutfit = new OutfitService(store, store, new SystemClock()).GetOutfit(userId, outfit.Id);

    AssertEqual(1, provider.Calls, "worker processing should call provider once.");
    AssertTrue(provider.LastOptions?.SequentialFlowEnabled == true, "worker should preserve sequential flow option from the queued job.");
    AssertEqual(TryOnStatus.Succeeded, completed?.Status, "processed job should succeed.");
    AssertEqual("https://example.com/output.jpg", completed?.OutputImageUrl, "processed job should store provider output.");
    AssertEqual("https://example.com/output.jpg", updatedOutfit?.PersonPreviewUrl, "processed job should update outfit preview.");
}

static void TestTryOnServiceForwardsSequentialFlowOption()
{
    var store = new InMemoryOutfitStore();
    var userId = "user-a";
    var top = store.CreateGarment(CreateGarment(userId, "white tee", GarmentCategory.Top));
    var outfit = new OutfitService(store, store, new SystemClock())
        .CreateOutfit(userId, "casual", new[] { top.Id });
    var provider = new CountingTryOnProvider();
    var service = new TryOnService(store, store, new RecordingTryOnJobQueue(), provider, new SystemClock());

    var job = service.StartAsync(userId, outfit.Id, "https://example.com/person.jpg", consentAccepted: true, sequentialFlowEnabled: true)
        .GetAwaiter()
        .GetResult();
    service.ProcessQueuedJobAsync(job.Id).GetAwaiter().GetResult();

    AssertEqual(1, provider.Calls, "provider should receive the try-on request");
    AssertTrue(provider.LastOptions?.SequentialFlowEnabled == true, "provider should receive sequential flow option");
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

static void TestImageProcessingPipelineContracts()
{
    AssertTrue(typeof(IImageProcessor).IsInterface, "application should expose an image processing port.");
    var variantNames = Enum.GetNames<StoredImageVariant>();
    foreach (var variant in new[] { "Original", "Thumbnail", "ProcessedCutout", "TryOnOutput", "PrivatePreview", "SegmentationMask" })
    {
        AssertTrue(variantNames.Contains(variant), $"stored image variant {variant} should be modeled.");
    }
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
        AssertEqual("image/png", stored.ContentType, "stored content type should be preserved");
        AssertTrue(stored.Length > 0, "stored length should reflect processed object bytes");
        AssertTrue(File.Exists(Path.Combine(tempPath, "garments", "original", stored.FileName)), "original garment object should exist on disk");
        AssertTrue(File.Exists(Path.Combine(tempPath, "garments", "thumbnail", stored.FileName)), "thumbnail garment object should exist on disk");
        AssertTrue(File.Exists(Path.Combine(tempPath, "garments", "processed-cutout", stored.FileName)), "processed cutout garment object should exist on disk");
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
        "is_deleted"
    })
    {
        AssertTrue(schema.Contains(column, StringComparison.OrdinalIgnoreCase), $"schema should include privacy/storage/auth field {column}.");
    }
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
        typeof(MockTryOnProvider)
    };

    foreach (var providerType in providerTypes)
    {
        AssertTrue(typeof(ITryOnProvider).IsAssignableFrom(providerType), $"{providerType.Name} should implement ITryOnProvider.");
    }
}

static void TestFashnProviderRequiresApiKey()
{
    var handler = new RecordingFashnHandler();
    var provider = new FashnTryOnProvider(
        new HttpClient(handler) { BaseAddress = new Uri("https://api.test/v1/") },
        new FashnTryOnSettings("", "tryon-v1.6", "balanced", 1, TimeSpan.Zero));

    AssertThrows<InvalidOperationException>(
        () => provider.Generate("user-a", CreateSingleGarmentOutfit(), "https://app.test/user.jpg", new TryOnOptions(false)),
        "fashn provider should require an API key");
    AssertEqual(0, handler.Requests.Count, "missing key must stop before network call");
}

static void TestFashnProviderSubmitsRequestAndPollsStatus()
{
    var handler = new RecordingFashnHandler();
    handler.EnqueueJson(HttpStatusCode.OK, "{\"id\":\"prediction-1\",\"error\":null}");
    handler.EnqueueJson(HttpStatusCode.OK, "{\"id\":\"prediction-1\",\"status\":\"completed\",\"output\":[\"https://cdn.fashn.ai/output.png\"],\"error\":null}");
    var provider = new FashnTryOnProvider(
        new HttpClient(handler) { BaseAddress = new Uri("https://api.test/v1/") },
        new FashnTryOnSettings("test-key", "tryon-v1.6", "performance", 2, TimeSpan.Zero));

    var generation = provider.Generate("user-a", CreateSingleGarmentOutfit(), "https://app.test/user.jpg", new TryOnOptions(false));

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
        new FashnTryOnSettings("test-key", "tryon-v1.6", "balanced", 1, TimeSpan.Zero));

    AssertThrows<InvalidOperationException>(
        () => provider.Generate("user-a", CreateTwoGarmentOutfit(), "https://app.test/user.jpg", new TryOnOptions(false)),
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
        new FashnTryOnSettings("test-key", "tryon-v1.6", "balanced", 2, TimeSpan.Zero));

    var generation = provider.Generate("user-a", CreateTwoGarmentOutfit(), "https://app.test/user.jpg", new TryOnOptions(true));

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

static byte[] MinimalPngBytes()
{
    return Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAANSURBVBhXY/j///9/AAn7A/0FQ0XKAAAAAElFTkSuQmCC");
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

static void AssertAssemblyDoesNotReference(IEnumerable<string> references, IEnumerable<string> forbidden, string message)
{
    var referenceSet = references.ToHashSet(StringComparer.Ordinal);
    var forbiddenMatches = forbidden.Where(referenceSet.Contains).ToList();

    if (forbiddenMatches.Count > 0)
    {
        throw new InvalidOperationException($"{message} Forbidden references: {string.Join(", ", forbiddenMatches)}.");
    }
}

sealed class CountingTryOnProvider : ITryOnProvider
{
    public int Calls { get; private set; }
    public TryOnOptions? LastOptions { get; private set; }
    public string Name => "test";

    public TryOnGeneration Generate(string userId, Outfit outfit, string bodyReferencePhotoUrl, TryOnOptions options)
    {
        Calls++;
        LastOptions = options;
        return new TryOnGeneration("test-provider-job", "https://example.com/output.jpg");
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
