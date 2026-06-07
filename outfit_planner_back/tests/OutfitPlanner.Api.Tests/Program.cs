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
    ("docker compose publishes postgres on a non-default host port", TestDockerComposePublishesPostgresOnNonDefaultHostPort),
    ("frontend docker config proxies api through same origin", TestFrontendDockerConfigProxiesApiThroughSameOrigin),
    ("postgres store implements application repository ports", TestPostgresStoreImplementsRepositoryPorts),
    ("postgres schema contains tables required by repository ports", TestPostgresSchemaContainsRepositoryTables),
    ("maps garment category to body zone", TestCategoryMapping),
    ("outfit service rejects two garments for the same category", TestDuplicateCategoryRejected),
    ("try-on service requires explicit AI consent before provider call", TestTryOnConsentRequired),
    ("try-on service forwards sequential flow option to provider", TestTryOnServiceForwardsSequentialFlowOption),
    ("schedule service stores one planned outfit per user and day", TestDailySchedulePerUser),
    ("share token generator emits url safe high entropy tokens", TestShareTokenGenerator),
    ("photo upload service rejects unsupported content types", TestPhotoUploadRejectsUnsupportedContentType),
    ("photo upload service accepts large phone photos", TestPhotoUploadAcceptsLargePhonePhotos),
    ("api configures upload body limits", TestApiConfiguresUploadBodyLimits),
    ("api exposes test diagnostics and trace ids", TestApiExposesTestDiagnosticsAndTraceIds),
    ("photo upload service stores garment photo and returns public url", TestPhotoUploadStoresGarmentPhoto),
    ("photo upload service stores body reference photo separately", TestPhotoUploadStoresBodyReferencePhoto),
    ("wardrobe service deletes garment records and stored photos", TestWardrobeServiceDeletesGarmentAndStoredPhoto),
    ("wardrobe service deletes body reference records and stored photos", TestWardrobeServiceDeletesBodyReferenceAndStoredPhoto),
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

static void TestDockerComposePublishesPostgresOnNonDefaultHostPort()
{
    var composePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "docker-compose.yml"));
    var compose = File.ReadAllText(composePath);

    AssertTrue(compose.Contains("\"5433:5432\"", StringComparison.Ordinal), "postgres should publish on host port 5433 to avoid colliding with local PostgreSQL on Windows.");
}

static void TestFrontendDockerConfigProxiesApiThroughSameOrigin()
{
    var rootPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".."));
    var compose = File.ReadAllText(Path.Combine(rootPath, "docker-compose.yml"));
    var nginx = File.ReadAllText(Path.Combine(rootPath, "outfit_planner_front", "nginx.conf"));

    AssertTrue(compose.Contains("VITE_API_URL: /api", StringComparison.Ordinal), "frontend docker build should use same-origin /api.");
    AssertTrue(!compose.Contains("VITE_API_URL: http://localhost:5000/api", StringComparison.Ordinal), "frontend docker build should not bake cross-origin localhost API URL.");
    AssertTrue(nginx.Contains("location /api/", StringComparison.Ordinal), "frontend nginx should proxy /api requests.");
    AssertTrue(nginx.Contains("proxy_pass http://api:8080/api/", StringComparison.Ordinal), "frontend nginx should proxy to the api service.");
    AssertTrue(nginx.Contains("client_max_body_size 100m", StringComparison.Ordinal), "frontend nginx should allow large photo uploads through the proxy.");
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

static void TestDuplicateCategoryRejected()
{
    var store = new InMemoryOutfitStore();
    var service = new OutfitService(store, store, new SystemClock());
    var topA = store.CreateGarment(CreateGarment("user-a", "white tee", GarmentCategory.Top));
    var topB = store.CreateGarment(CreateGarment("user-a", "black knit", GarmentCategory.Top));

    AssertThrows<InvalidOperationException>(
        () => service.CreateOutfit("user-a", "bad outfit", new[] { topA.Id, topB.Id }),
        "duplicate categories must be rejected");
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
    var service = new TryOnService(store, store, provider, new SystemClock());

    AssertThrows<InvalidOperationException>(
        () => service.Start(userId, outfit.Id, "https://example.com/person.jpg", consentAccepted: false),
        "try-on should require consent");
    AssertEqual(0, provider.Calls, "provider must not receive photos without consent");
}

static void TestTryOnServiceForwardsSequentialFlowOption()
{
    var store = new InMemoryOutfitStore();
    var userId = "user-a";
    var top = store.CreateGarment(CreateGarment(userId, "white tee", GarmentCategory.Top));
    var outfit = new OutfitService(store, store, new SystemClock())
        .CreateOutfit(userId, "casual", new[] { top.Id });
    var provider = new CountingTryOnProvider();
    var service = new TryOnService(store, store, provider, new SystemClock());

    service.Start(userId, outfit.Id, "https://example.com/person.jpg", consentAccepted: true, sequentialFlowEnabled: true);

    AssertEqual(1, provider.Calls, "provider should receive the try-on request");
    AssertTrue(provider.LastOptions?.SequentialFlowEnabled == true, "provider should receive sequential flow option");
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

static void TestPhotoUploadStoresGarmentPhoto()
{
    var tempPath = Path.Combine(Path.GetTempPath(), "outfit-planner-photo-tests", Guid.NewGuid().ToString("N"));

    try
    {
        var bytes = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
        var storage = new LocalPhotoStorage(tempPath);
        var service = new PhotoUploadService(storage);

        var stored = service.UploadGarmentPhoto(new IncomingPhoto("shirt.png", "image/png", bytes.Length, new MemoryStream(bytes)));

        AssertTrue(stored.Url.StartsWith("/uploads/garments/", StringComparison.Ordinal), "stored photo should expose public upload path");
        AssertEqual("image/png", stored.ContentType, "stored content type should be preserved");
        AssertEqual(bytes.Length, (int)stored.Length, "stored length should be preserved");
        AssertTrue(File.Exists(Path.Combine(tempPath, "garments", stored.FileName)), "uploaded file should exist on disk");
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
        var bytes = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
        var storage = new LocalPhotoStorage(tempPath);
        var service = new PhotoUploadService(storage);

        var stored = service.UploadBodyReferencePhoto(new IncomingPhoto("body.png", "image/png", bytes.Length, new MemoryStream(bytes)));

        AssertTrue(stored.Url.StartsWith("/uploads/body-reference-photos/", StringComparison.Ordinal), "stored body photo should expose body reference upload path");
        AssertEqual("image/png", stored.ContentType, "stored content type should be preserved");
        AssertTrue(File.Exists(Path.Combine(tempPath, "body-reference-photos", stored.FileName)), "uploaded body photo should exist on disk");
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
            .UploadGarmentPhoto(new IncomingPhoto("shirt.png", "image/png", 8, new MemoryStream(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })));
        var garment = service.CreateGarment(new CreateGarmentCommand(
            "user-a",
            "linen shirt",
            GarmentCategory.Top,
            $"http://localhost:5000{stored.Url}",
            $"http://localhost:5000{stored.Url}",
            Array.Empty<string>()));

        var deleted = service.DeleteGarment("user-a", garment.Id);

        AssertTrue(deleted, "existing garment should be deleted");
        AssertEqual(0, service.ListGarments("user-a").Count, "deleted garment should disappear from wardrobe");
        AssertTrue(!File.Exists(Path.Combine(tempPath, "garments", stored.FileName)), "deleted garment should remove its stored photo");
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
            .UploadBodyReferencePhoto(new IncomingPhoto("body.png", "image/png", 8, new MemoryStream(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })));
        var photo = service.CreateBodyReferencePhoto("user-a", $"http://localhost:5000{stored.Url}");

        var deleted = service.DeleteBodyReferencePhoto("user-a", photo.Id);

        AssertTrue(deleted, "existing body reference should be deleted");
        AssertEqual(0, service.ListBodyReferencePhotos("user-a").Count, "deleted body reference should disappear from the library");
        AssertTrue(!File.Exists(Path.Combine(tempPath, "body-reference-photos", stored.FileName)), "deleted body reference should remove its stored photo");
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

    public TryOnGeneration Generate(string userId, Outfit outfit, string bodyReferencePhotoUrl, TryOnOptions options)
    {
        Calls++;
        LastOptions = options;
        return new TryOnGeneration("test-provider-job", "https://example.com/output.jpg");
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
