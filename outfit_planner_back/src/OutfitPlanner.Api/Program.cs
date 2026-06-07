using OutfitPlanner.Application.Abstractions;
using OutfitPlanner.Application.Services;
using OutfitPlanner.Api.Contracts;
using OutfitPlanner.Infrastructure.Diagnostics;
using OutfitPlanner.Infrastructure.Security;
using OutfitPlanner.Infrastructure.Storage;
using OutfitPlanner.Infrastructure.TryOn;
using Microsoft.AspNetCore.Http.Features;
using Npgsql;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
const long MaxUploadRequestBytes = PhotoUploadService.MaxPhotoBytes * 2;

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = MaxUploadRequestBytes;
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = MaxUploadRequestBytes;
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy
            .WithOrigins(
                "http://localhost:5173",
                "http://127.0.0.1:5173",
                "http://localhost:4173",
                "http://127.0.0.1:4173")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<IShareTokenGenerator, SecureShareTokenGenerator>();
builder.Services.AddHttpClient("fashn", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Fashn:BaseUrl"] ?? "https://api.fashn.ai/v1/");
    client.Timeout = TimeSpan.FromSeconds(builder.Configuration.GetValue("Fashn:TimeoutSeconds", 180));
});
builder.Services.AddSingleton<ITryOnProvider>(provider =>
{
    var configuredProvider = builder.Configuration["TryOn:Provider"];
    if (!string.Equals(configuredProvider, "Fashn", StringComparison.OrdinalIgnoreCase))
    {
        return new MockTryOnProvider();
    }

    var http = provider.GetRequiredService<IHttpClientFactory>().CreateClient("fashn");
    var settings = new FashnTryOnSettings(
        builder.Configuration["Fashn:ApiKey"] ?? "",
        builder.Configuration["Fashn:ModelName"] ?? "tryon-v1.6",
        builder.Configuration["Fashn:Mode"] ?? "balanced",
        builder.Configuration.GetValue("Fashn:MaxPollingAttempts", 30),
        TimeSpan.FromSeconds(builder.Configuration.GetValue("Fashn:PollIntervalSeconds", 2)));
    return new FashnTryOnProvider(http, settings);
});
builder.Services.AddSingleton(_ => new LocalPhotoStorage(Path.Combine(builder.Environment.ContentRootPath, "storage", "garment-photos")));
builder.Services.AddSingleton<IPhotoStorage>(provider => provider.GetRequiredService<LocalPhotoStorage>());
builder.Services.AddSingleton<IStoredPhotoReader>(provider => provider.GetRequiredService<LocalPhotoStorage>());
builder.Services.AddSingleton<IStoredPhotoDeletion>(provider => provider.GetRequiredService<LocalPhotoStorage>());
var postgresConnectionString = builder.Configuration.GetConnectionString("Postgres");
var storageProvider = string.IsNullOrWhiteSpace(postgresConnectionString) ? "InMemory" : "Postgres";
if (storageProvider == "Postgres")
{
    builder.Services.AddSingleton(NpgsqlDataSource.Create(postgresConnectionString!));
    builder.Services.AddSingleton(provider =>
    {
        var schemaPath = Path.Combine(AppContext.BaseDirectory, "database", "schema.sql");
        return new PostgresSchemaInitializer(provider.GetRequiredService<NpgsqlDataSource>(), schemaPath);
    });
    builder.Services.AddSingleton<PostgresOutfitStore>();
    builder.Services.AddSingleton<IBodyReferencePhotoRepository>(provider => provider.GetRequiredService<PostgresOutfitStore>());
    builder.Services.AddSingleton<IGarmentRepository>(provider => provider.GetRequiredService<PostgresOutfitStore>());
    builder.Services.AddSingleton<IOutfitRepository>(provider => provider.GetRequiredService<PostgresOutfitStore>());
    builder.Services.AddSingleton<IOutfitScheduleRepository>(provider => provider.GetRequiredService<PostgresOutfitStore>());
    builder.Services.AddSingleton<ITryOnJobRepository>(provider => provider.GetRequiredService<PostgresOutfitStore>());
    builder.Services.AddSingleton<IShareLinkRepository>(provider => provider.GetRequiredService<PostgresOutfitStore>());
}
else
{
    builder.Services.AddSingleton<InMemoryOutfitStore>();
    builder.Services.AddSingleton<IBodyReferencePhotoRepository>(provider => provider.GetRequiredService<InMemoryOutfitStore>());
    builder.Services.AddSingleton<IGarmentRepository>(provider => provider.GetRequiredService<InMemoryOutfitStore>());
    builder.Services.AddSingleton<IOutfitRepository>(provider => provider.GetRequiredService<InMemoryOutfitStore>());
    builder.Services.AddSingleton<IOutfitScheduleRepository>(provider => provider.GetRequiredService<InMemoryOutfitStore>());
    builder.Services.AddSingleton<ITryOnJobRepository>(provider => provider.GetRequiredService<InMemoryOutfitStore>());
    builder.Services.AddSingleton<IShareLinkRepository>(provider => provider.GetRequiredService<InMemoryOutfitStore>());
}
builder.Services.AddSingleton<WardrobeService>();
builder.Services.AddSingleton<PhotoUploadService>();
builder.Services.AddSingleton<OutfitService>();
builder.Services.AddSingleton<ScheduleService>();
builder.Services.AddSingleton<TryOnService>();
builder.Services.AddSingleton<ShareService>();
builder.Services.AddSingleton<PostgresConnectionProbe>();

var app = builder.Build();

app.Services.GetService<PostgresSchemaInitializer>()?.Initialize();

var detailedErrorsEnabled = app.Environment.IsDevelopment()
    || app.Environment.IsEnvironment("Test")
    || builder.Configuration.GetValue("DetailedErrors", false);

app.Use(async (context, next) =>
{
    var traceId = context.TraceIdentifier;
    context.Response.Headers["X-Trace-Id"] = traceId;

    try
    {
        await next(context);
    }
    catch (Exception ex)
    {
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("OutfitPlanner.Diagnostics");
        logger.LogError(ex, "Unhandled request failure {Method} {Path} trace {TraceId}", context.Request.Method, context.Request.Path, traceId);

        if (context.Response.HasStarted)
        {
            throw;
        }

        var body = new Dictionary<string, object?>
        {
            ["error"] = "Unhandled API error.",
            ["traceId"] = traceId
        };

        if (detailedErrorsEnabled)
        {
            body["detail"] = ex.Message;
            body["exception"] = ex.GetType().Name;
            body["method"] = context.Request.Method;
            body["path"] = context.Request.Path.ToString();
        }

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await Results.Json(body).ExecuteAsync(context);
    }
});

app.UseCors();

var api = app.MapGroup("/api");

api.MapGet("/health", () => Results.Ok(new { status = "ok", service = "outfit-planner-api" }));

api.MapGet("/system/status", async (PostgresConnectionProbe postgres, CancellationToken cancellationToken) =>
{
    var postgresStatus = await postgres.CheckAsync(cancellationToken);
    return Results.Ok(new
    {
        api = "running",
        storage = storageProvider,
        postgres = postgresStatus,
        aiProvider = builder.Configuration["TryOn:Provider"] ?? "Mock"
    });
});

api.MapGet("/auth/providers", () => Results.Ok(new[]
{
    new { id = "google", label = "Google OAuth", configured = false, demoHeader = "X-Demo-User" }
}));

api.MapPost("/body-reference-photos", (CreateBodyReferencePhotoRequest request, WardrobeService wardrobe, HttpContext context) =>
{
    try
    {
        var photo = wardrobe.CreateBodyReferencePhoto(CurrentUser(context), request.ImageUrl);
        return Results.Created($"/api/body-reference-photos/{photo.Id}", photo);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

api.MapGet("/body-reference-photos", (WardrobeService wardrobe, HttpContext context) =>
    Results.Ok(wardrobe.ListBodyReferencePhotos(CurrentUser(context))));

api.MapDelete("/body-reference-photos/{photoId:guid}", (Guid photoId, WardrobeService wardrobe, HttpContext context) =>
    wardrobe.DeleteBodyReferencePhoto(CurrentUser(context), photoId) ? Results.NoContent() : Results.NotFound());

api.MapGet("/garments", (WardrobeService wardrobe, HttpContext context) =>
    Results.Ok(wardrobe.ListGarments(CurrentUser(context))));

api.MapPost("/uploads/garment-photo", async (HttpRequest request, PhotoUploadService photos, ILogger<Program> logger, CancellationToken cancellationToken) =>
{
    return await UploadPhoto(request, logger, "garment", cancellationToken, photo => photos.UploadGarmentPhoto(photo));
});

api.MapPost("/uploads/body-reference-photo", async (HttpRequest request, PhotoUploadService photos, ILogger<Program> logger, CancellationToken cancellationToken) =>
{
    return await UploadPhoto(request, logger, "body-reference", cancellationToken, photo => photos.UploadBodyReferencePhoto(photo));
});

api.MapPost("/garments", (CreateGarmentRequest request, WardrobeService wardrobe, HttpContext context) =>
{
    try
    {
        var garment = wardrobe.CreateGarment(new CreateGarmentCommand(
            CurrentUser(context),
            request.Name,
            request.Category,
            request.ImageUrl,
            request.ThumbnailUrl,
            request.Tags ?? Array.Empty<string>()));

        return Results.Created($"/api/garments/{garment.Id}", garment);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

api.MapDelete("/garments/{garmentId:guid}", (Guid garmentId, WardrobeService wardrobe, HttpContext context) =>
    wardrobe.DeleteGarment(CurrentUser(context), garmentId) ? Results.NoContent() : Results.NotFound());

api.MapGet("/outfits", (OutfitService outfits, HttpContext context) =>
    Results.Ok(outfits.ListOutfits(CurrentUser(context))));

api.MapPost("/outfits", (CreateOutfitRequest request, OutfitService outfits, HttpContext context) =>
{
    try
    {
        var outfit = outfits.CreateOutfit(CurrentUser(context), request.Name, request.GarmentIds);
        return Results.Created($"/api/outfits/{outfit.Id}", outfit);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

api.MapPost("/outfits/{outfitId:guid}/try-on", (
    Guid outfitId,
    StartTryOnRequest request,
    TryOnService tryOn,
    HttpContext context) =>
{
    try
    {
        var job = tryOn.Start(CurrentUser(context), outfitId, request.BodyReferencePhotoUrl, request.ConsentAccepted, request.SequentialFlowEnabled);
        return Results.Accepted($"/api/try-on-jobs/{job.Id}", job);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

api.MapGet("/try-on-jobs/{jobId:guid}", (Guid jobId, TryOnService tryOn, HttpContext context) =>
{
    var job = tryOn.GetJob(CurrentUser(context), jobId);
    return job is null ? Results.NotFound() : Results.Ok(job);
});

api.MapPost("/schedule", (ScheduleOutfitRequest request, ScheduleService schedule, HttpContext context) =>
{
    try
    {
        var scheduled = schedule.ScheduleOutfit(CurrentUser(context), request.Date, request.OutfitId);
        return Results.Ok(scheduled);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

api.MapGet("/schedule", (string from, string to, ScheduleService schedule, HttpContext context) =>
{
    if (!DateOnly.TryParse(from, out var fromDate) || !DateOnly.TryParse(to, out var toDate))
    {
        return Results.BadRequest(new { error = "Query parameters 'from' and 'to' must be ISO dates." });
    }

    try
    {
        return Results.Ok(schedule.GetSchedule(CurrentUser(context), fromDate, toDate));
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

api.MapPost("/outfits/{outfitId:guid}/share", (Guid outfitId, ShareService share, HttpContext context) =>
{
    try
    {
        var link = share.CreateShareLink(CurrentUser(context), outfitId);
        return Results.Ok(new ShareLinkResponse(link.Token, $"/share/{link.Token}"));
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

api.MapGet("/share/{token}", (string token, ShareService share) =>
{
    var outfit = share.GetSharedOutfit(token);
    return outfit is null ? Results.NotFound() : Results.Ok(new
    {
        outfit.Id,
        outfit.Name,
        outfit.Items,
        outfit.ClothesOnlyPreviewUrl,
        outfit.PersonPreviewUrl,
        outfit.CreatedAt
    });
});

app.MapGet("/uploads/garments/{fileName}", (string fileName, IStoredPhotoReader photos) =>
{
    var photo = photos.GetGarmentPhoto(fileName);
    return photo is null ? Results.NotFound() : Results.File(photo.FullPath, photo.ContentType);
});

app.MapGet("/uploads/body-reference-photos/{fileName}", (string fileName, IStoredPhotoReader photos) =>
{
    var photo = photos.GetBodyReferencePhoto(fileName);
    return photo is null ? Results.NotFound() : Results.File(photo.FullPath, photo.ContentType);
});

app.Run();

static async Task<IResult> UploadPhoto(HttpRequest request, ILogger logger, string uploadKind, CancellationToken cancellationToken, Func<IncomingPhoto, StoredPhoto> store)
{
    logger.LogInformation(
        "Upload diagnostics: received {Kind} upload request trace {TraceId}; contentType={ContentType}; contentLength={ContentLength}; host={Host}; origin={Origin}",
        uploadKind,
        request.HttpContext.TraceIdentifier,
        request.ContentType,
        request.ContentLength,
        request.Host.ToString(),
        request.Headers.Origin.ToString());

    if (!request.HasFormContentType)
    {
        logger.LogWarning("Upload diagnostics: rejected {Kind} upload trace {TraceId}; request was not multipart form data", uploadKind, request.HttpContext.TraceIdentifier);
        return Results.BadRequest(new { error = "Upload must use multipart form data." });
    }

    var form = await request.ReadFormAsync(cancellationToken);
    var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
    if (file is null)
    {
        logger.LogWarning("Upload diagnostics: rejected {Kind} upload trace {TraceId}; no file part was provided", uploadKind, request.HttpContext.TraceIdentifier);
        return Results.BadRequest(new { error = "Photo file is required." });
    }

    try
    {
        logger.LogInformation(
            "Upload diagnostics: processing {Kind} upload trace {TraceId}; fileName={FileName}; fileContentType={FileContentType}; fileLength={FileLength}",
            uploadKind,
            request.HttpContext.TraceIdentifier,
            file.FileName,
            file.ContentType,
            file.Length);

        await using var stream = file.OpenReadStream();
        var stored = store(new IncomingPhoto(file.FileName, file.ContentType, file.Length, stream));
        var publicUrl = $"{request.Scheme}://{request.Host}{stored.Url}";
        logger.LogInformation(
            "Upload diagnostics: stored {Kind} upload trace {TraceId}; storedFileName={StoredFileName}; publicUrl={PublicUrl}",
            uploadKind,
            request.HttpContext.TraceIdentifier,
            stored.FileName,
            publicUrl);
        return Results.Created(publicUrl, new UploadedPhotoResponse(stored.FileName, stored.ContentType, stored.Length, publicUrl));
    }
    catch (InvalidOperationException ex)
    {
        logger.LogWarning(
            ex,
            "Upload diagnostics: rejected {Kind} upload trace {TraceId}; fileName={FileName}; fileContentType={FileContentType}; fileLength={FileLength}",
            uploadKind,
            request.HttpContext.TraceIdentifier,
            file.FileName,
            file.ContentType,
            file.Length);
        return Results.BadRequest(new { error = ex.Message });
    }
}

static string CurrentUser(HttpContext context)
{
    if (!context.Request.Headers.TryGetValue("X-Demo-User", out var header))
    {
        return "demo-user";
    }

    var candidate = header.ToString().Trim();
    if (candidate.Length is < 1 or > 100)
    {
        return "demo-user";
    }

    return candidate.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.' or '@')
        ? candidate
        : "demo-user";
}

public partial class Program;
