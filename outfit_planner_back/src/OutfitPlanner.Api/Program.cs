using OutfitPlanner.Application.Abstractions;
using OutfitPlanner.Application.Services;
using OutfitPlanner.Api;
using OutfitPlanner.Api.Authentication;
using OutfitPlanner.Api.Contracts;
using OutfitPlanner.Domain;
using OutfitPlanner.Infrastructure.Diagnostics;
using OutfitPlanner.Infrastructure.Security;
using OutfitPlanner.Infrastructure.Storage;
using OutfitPlanner.Infrastructure.TryOn;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Npgsql;
using StackExchange.Redis;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
const long MaxUploadRequestBytes = PhotoUploadService.MaxPhotoBytes * 2;
const string SessionCookieName = "outfit_session";
const string CsrfCookieName = "outfit_csrf";
const string ExternalAuthCookieScheme = "outfit_external";
const string CurrentUserItemKey = "outfit.current_user_id";
const string CsrfHeaderName = "X-CSRF-Token";

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
builder.Services.AddOpenApi();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedHost
        | ForwardedHeaders.XForwardedProto;

    if (builder.Environment.IsDevelopment())
    {
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    }
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy
            .WithOrigins(
                "http://localhost:5173",
                "http://127.0.0.1:5173",
                "http://localhost:4173",
                "http://127.0.0.1:4173",
                "https://localhost:5173",
                "https://127.0.0.1:5173",
                "https://localhost:4173",
                "https://127.0.0.1:4173")
            .AllowCredentials()
            .AllowAnyHeader()
            .AllowAnyMethod());
});
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("login-rate-limit", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(5),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            }));
    options.AddPolicy("registration-rate-limit", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            }));
});

builder.Services.AddSingleton<IClock, OutfitPlanner.Infrastructure.Security.SystemClock>();
builder.Services.AddSingleton<IShareTokenGenerator, SecureShareTokenGenerator>();
builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddSingleton<IAuthTokenService, SecureAuthTokenService>();
var authenticationBuilder = builder.Services.AddAuthentication();
var externalAuthPublicOrigin = NormalizePublicOrigin(builder.Configuration["Authentication:PublicOrigin"]);
authenticationBuilder.AddCookie(ExternalAuthCookieScheme, options =>
{
    options.Cookie.Name = ExternalAuthCookieScheme;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
});

var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
var googleConfigured = !string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret);
if (googleConfigured)
{
    authenticationBuilder.AddOAuth<GoogleOptions, CanonicalGoogleHandler>("google", options =>
    {
        options.ClientId = googleClientId!;
        options.ClientSecret = googleClientSecret!;
        options.SignInScheme = ExternalAuthCookieScheme;
        options.CallbackPath = "/api/auth/external/google/callback";
        options.AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
        options.TokenEndpoint = "https://oauth2.googleapis.com/token";
        options.UserInformationEndpoint = "https://openidconnect.googleapis.com/v1/userinfo";
        options.Scope.Add("openid");
        options.Scope.Add("email");
        options.Scope.Add("profile");
        options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "sub");
        options.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");
        options.ClaimActions.MapJsonKey(ClaimTypes.Name, "name");
        options.ClaimActions.MapJsonKey("email_verified", "email_verified");
        options.Events = new OAuthEvents
        {
            OnRedirectToAuthorizationEndpoint = context =>
            {
                context.Response.Redirect(BuildOAuthAuthorizationRedirectUri(context, externalAuthPublicOrigin));
                return Task.CompletedTask;
            },
            OnRemoteFailure = context =>
            {
                var message = context.Failure?.Message ?? "External authentication failed.";
                context.Response.Redirect($"/api/auth/external/google/complete?externalError={Uri.EscapeDataString(message)}");
                context.HandleResponse();
                return Task.CompletedTask;
            }
        };
    });
}

var appleClientId = builder.Configuration["Authentication:Apple:ClientId"];
var appleClientSecret = builder.Configuration["Authentication:Apple:ClientSecret"];
var appleConfigured = !string.IsNullOrWhiteSpace(appleClientId) && !string.IsNullOrWhiteSpace(appleClientSecret);
if (appleConfigured)
{
    authenticationBuilder.AddOpenIdConnect("apple", options =>
    {
        options.Authority = "https://appleid.apple.com";
        options.ClientId = appleClientId!;
        options.ClientSecret = appleClientSecret!;
        options.SignInScheme = ExternalAuthCookieScheme;
        options.CallbackPath = "/api/auth/external/apple/callback";
        options.ResponseType = "code";
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("email");
        options.Scope.Add("name");
        options.SaveTokens = false;
        options.GetClaimsFromUserInfoEndpoint = false;
        options.Events = new OpenIdConnectEvents
        {
            OnRedirectToIdentityProvider = context =>
            {
                if (externalAuthPublicOrigin is not null)
                {
                    context.ProtocolMessage.RedirectUri = BuildExternalCallbackUri(
                        externalAuthPublicOrigin,
                        context.Options.CallbackPath);
                }

                return Task.CompletedTask;
            }
        };
    });
}
builder.Services.AddHttpClient("fashn", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Fashn:BaseUrl"] ?? "https://api.fashn.ai/v1/");
    client.Timeout = TimeSpan.FromSeconds(builder.Configuration.GetValue("Fashn:TimeoutSeconds", 180));
});
builder.Services.AddHttpClient("local-vton");
builder.Services.AddHttpClient("local-cat-vton");
builder.Services.AddHttpClient("replicate");
builder.Services.AddHttpClient("fal");
builder.Services.AddSingleton<ITryOnProvider>(provider => CreateTryOnProvider(provider, builder.Configuration));
var redisConnectionString = builder.Configuration["ConnectionStrings:Redis"] ?? builder.Configuration.GetConnectionString("Redis");
if (string.IsNullOrWhiteSpace(redisConnectionString))
{
    builder.Services.AddSingleton<ITryOnJobQueue, InMemoryTryOnJobQueue>();
}
else
{
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnectionString));
    builder.Services.AddSingleton<ITryOnJobQueue>(provider => new RedisTryOnJobQueue(provider.GetRequiredService<IConnectionMultiplexer>()));
}
builder.Services.AddSingleton<IObjectStorage>(provider => CreateObjectStorage(builder.Configuration, builder.Environment));
builder.Services.AddSingleton<IImageProcessor, ImageProcessor>();
builder.Services.AddSingleton<LocalPhotoStorage>();
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
        var migrationsPath = Path.Combine(AppContext.BaseDirectory, "database", "migrations");
        return new PostgresMigrationRunner(
            postgresConnectionString!,
            migrationsPath,
            provider.GetRequiredService<ILogger<PostgresMigrationRunner>>());
    });
    builder.Services.AddSingleton<PostgresOutfitStore>();
    builder.Services.AddSingleton<IBodyReferencePhotoRepository>(provider => provider.GetRequiredService<PostgresOutfitStore>());
    builder.Services.AddSingleton<IGarmentRepository>(provider => provider.GetRequiredService<PostgresOutfitStore>());
    builder.Services.AddSingleton<IOutfitRepository>(provider => provider.GetRequiredService<PostgresOutfitStore>());
    builder.Services.AddSingleton<IOutfitScheduleRepository>(provider => provider.GetRequiredService<PostgresOutfitStore>());
    builder.Services.AddSingleton<ITryOnJobRepository>(provider => provider.GetRequiredService<PostgresOutfitStore>());
    builder.Services.AddSingleton<IShareLinkRepository>(provider => provider.GetRequiredService<PostgresOutfitStore>());
    builder.Services.AddSingleton<IUserAccountRepository>(provider => provider.GetRequiredService<PostgresOutfitStore>());
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
    builder.Services.AddSingleton<IUserAccountRepository>(provider => provider.GetRequiredService<InMemoryOutfitStore>());
}
builder.Services.AddSingleton<WardrobeService>();
builder.Services.AddSingleton<PhotoUploadService>();
builder.Services.AddSingleton<OutfitService>();
builder.Services.AddSingleton<ScheduleService>();
builder.Services.AddSingleton<TryOnService>();
builder.Services.AddSingleton<ShareService>();
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<PostgresConnectionProbe>();
builder.Services.AddHostedService<TryOnBackgroundWorker>();

var app = builder.Build();

if (!IsOpenApiDocumentGeneration())
{
    app.Services.GetService<PostgresMigrationRunner>()?.Initialize();
}

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

app.UseForwardedHeaders();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.Use(async (context, next) =>
{
    if (!RequiresAuthenticatedUser(context))
    {
        await next(context);
        return;
    }

    var auth = context.RequestServices.GetRequiredService<AuthService>();
    var sessionToken = context.Request.Cookies[SessionCookieName];
    var requireCsrf = RequiresCsrfToken(context.Request);
    var session = auth.AuthenticateSession(
        sessionToken,
        requireCsrf ? context.Request.Headers[CsrfHeaderName].ToString() : null,
        requireCsrf);

    if (session is null)
    {
        if (requireCsrf && auth.AuthenticateSession(sessionToken, null, requireCsrf: false) is not null)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await Results.Json(new { error = "CSRF token is required for this request." }).ExecuteAsync(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await Results.Json(new { error = "Authentication is required." }).ExecuteAsync(context);
        return;
    }

    context.Items[CurrentUserItemKey] = session.User.Id;
    await next(context);
});

var api = app.MapGroup("/api");
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Test"))
{
    app.MapOpenApi("/api/openapi/{documentName}.json");
}

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
    new AuthProviderResponse("email", "Email", true, "password"),
    new AuthProviderResponse("google", "Google", googleConfigured, "oauth"),
    new AuthProviderResponse("apple", "Apple", appleConfigured, "oidc")
}));

api.MapPost("/auth/register", (RegisterRequest request, AuthService auth, HttpContext context) =>
{
    try
    {
        var result = auth.RegisterWithPassword(request.Email, request.Password, request.RepeatPassword);
        IssueAuthCookies(context, result, app.Environment);
        return Results.Ok(ToAuthSessionResponse(result));
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).RequireRateLimiting("registration-rate-limit");

api.MapPost("/auth/login", (LoginRequest request, AuthService auth, HttpContext context) =>
{
    try
    {
        var result = auth.SignInWithPassword(request.Email, request.Password);
        IssueAuthCookies(context, result, app.Environment);
        return Results.Ok(ToAuthSessionResponse(result));
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).RequireRateLimiting("login-rate-limit");

api.MapPost("/auth/logout", (AuthService auth, HttpContext context) =>
{
    auth.RevokeSession(context.Request.Cookies[SessionCookieName]);
    ClearAuthCookies(context, app.Environment);
    return Results.Ok(new { status = "signed-out" });
});

api.MapGet("/auth/me", (AuthService auth, HttpContext context) =>
{
    var session = auth.AuthenticateSession(context.Request.Cookies[SessionCookieName], null, requireCsrf: false);
    return session is null ? Results.Unauthorized() : Results.Ok(ToAuthSessionResponseFromSession(session));
});

api.MapPost("/auth/email-verification/request", (EmailVerificationRequest request, AuthService auth) =>
{
    try
    {
        var token = auth.CreateEmailVerificationToken(request.Email);
        return Results.Ok(new { status = "verification-requested", token = app.Environment.IsDevelopment() ? token : null });
    }
    catch (InvalidOperationException)
    {
        return Results.Ok(new { status = "verification-requested" });
    }
}).RequireRateLimiting("login-rate-limit");

api.MapPost("/auth/email-verification/confirm", (TokenRequest request, AuthService auth) =>
    auth.ConfirmEmailVerification(request.Token)
        ? Results.Ok(new { status = "email-verified" })
        : Results.BadRequest(new { error = "Verification token is invalid or expired." }));

api.MapPost("/auth/password-reset/request", (PasswordResetRequest request, AuthService auth) =>
{
    try
    {
        var token = auth.CreatePasswordResetToken(request.Email);
        return Results.Ok(new { status = "password-reset-requested", token = app.Environment.IsDevelopment() ? token : null });
    }
    catch (InvalidOperationException)
    {
        return Results.Ok(new { status = "password-reset-requested" });
    }
}).RequireRateLimiting("login-rate-limit");

api.MapPost("/auth/password-reset/confirm", (PasswordResetConfirmRequest request, AuthService auth) =>
{
    try
    {
        return auth.ResetPassword(request.Token, request.Password, request.RepeatPassword)
            ? Results.Ok(new { status = "password-reset" })
            : Results.BadRequest(new { error = "Password reset token is invalid or expired." });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

api.MapGet("/auth/sessions", (AuthService auth, HttpContext context) =>
{
    try
    {
        return Results.Ok(auth.ListSessions(context.Request.Cookies[SessionCookieName]));
    }
    catch (InvalidOperationException)
    {
        return Results.Unauthorized();
    }
});

api.MapDelete("/auth/sessions", (AuthService auth, HttpContext context) =>
{
    try
    {
        auth.RevokeAllSessions(context.Request.Cookies[SessionCookieName]);
        ClearAuthCookies(context, app.Environment);
        return Results.Ok(new { status = "sessions-revoked" });
    }
    catch (InvalidOperationException)
    {
        return Results.Unauthorized();
    }
});

api.MapGet("/auth/external/{provider}/start", (string provider, string? returnUrl) =>
{
    if (!TryNormalizeExternalProvider(provider, out var normalizedProvider))
    {
        return Results.BadRequest(new { error = "Unsupported external auth provider." });
    }

    if (!IsExternalProviderConfigured(normalizedProvider, googleConfigured, appleConfigured))
    {
        return Results.BadRequest(new { error = $"{normalizedProvider} authentication is not configured." });
    }

    var safeReturnUrl = NormalizeReturnUrl(returnUrl);
    var properties = new AuthenticationProperties
    {
        RedirectUri = $"/api/auth/external/{normalizedProvider}/complete?returnUrl={Uri.EscapeDataString(safeReturnUrl)}"
    };

    return Results.Challenge(properties, new[] { normalizedProvider });
});

api.MapGet("/auth/external/{provider}/complete", async (
    string provider,
    string? returnUrl,
    string? externalError,
    AuthService auth,
    HttpContext context,
    ILogger<Program> logger) =>
{
    if (!TryNormalizeExternalProvider(provider, out var normalizedProvider))
    {
        return Results.BadRequest(new { error = "Unsupported external auth provider." });
    }

    if (!string.IsNullOrWhiteSpace(externalError))
    {
        logger.LogWarning("External authentication failed for {Provider}: {Error}", normalizedProvider, externalError);
        return Results.BadRequest(new { error = "External authentication failed.", detail = externalError });
    }

    var external = await context.AuthenticateAsync(ExternalAuthCookieScheme);
    if (!external.Succeeded || external.Principal is null)
    {
        if (external.Failure is not null)
        {
            logger.LogWarning(external.Failure, "External authentication cookie was not valid for {Provider}", normalizedProvider);
            return Results.BadRequest(new
            {
                error = "External authentication did not complete.",
                detail = detailedErrorsEnabled ? external.Failure.Message : null
            });
        }

        return Results.BadRequest(new { error = "External authentication did not complete." });
    }

    var subject = external.Principal.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? external.Principal.FindFirstValue("sub");
    if (string.IsNullOrWhiteSpace(subject))
    {
        return Results.BadRequest(new { error = "External authentication did not return a stable subject." });
    }

    var email = external.Principal.FindFirstValue(ClaimTypes.Email);
    var displayName = external.Principal.FindFirstValue(ClaimTypes.Name);
    var emailVerified = IsExternalEmailVerified(external.Principal);
    try
    {
        var result = auth.SignInWithExternalAccount(new ExternalSignInCommand(
            normalizedProvider,
            subject,
            email,
            emailVerified,
            displayName));

        await context.SignOutAsync(ExternalAuthCookieScheme);
        IssueAuthCookies(context, result, app.Environment);
        return Results.Redirect(NormalizeReturnUrl(returnUrl));
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "External authentication callback failed for {Provider}", normalizedProvider);
        return Results.BadRequest(new
        {
            error = "External authentication callback failed.",
            detail = detailedErrorsEnabled ? ex.Message : null
        });
    }
});

api.MapGet("/account/export", (
    IUserAccountRepository users,
    IGarmentRepository garments,
    IOutfitRepository outfits,
    IBodyReferencePhotoRepository bodyPhotos,
    ITryOnJobRepository tryOnJobs,
    HttpContext context) =>
{
    var userId = CurrentUser(context);
    return Results.Ok(new
    {
        user = users.GetUserById(userId),
        garments = garments.ListGarmentsByUser(userId),
        outfits = outfits.ListOutfitsByUser(userId),
        bodyReferencePhotos = bodyPhotos.ListBodyReferencePhotosByUser(userId),
        tryOnJobs = tryOnJobs.ListTryOnJobsByUser(userId)
    });
});

api.MapDelete("/account", (IUserAccountRepository users, HttpContext context) =>
{
    var deleted = users.DeleteUserById(CurrentUser(context));
    ClearAuthCookies(context, app.Environment);
    return deleted ? Results.NoContent() : Results.NotFound();
});

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
})
    .Produces<BodyReferencePhoto>(StatusCodes.Status201Created);

api.MapGet("/body-reference-photos", (WardrobeService wardrobe, HttpContext context) =>
    Results.Ok(wardrobe.ListBodyReferencePhotos(CurrentUser(context))))
    .Produces<IReadOnlyList<BodyReferencePhoto>>(StatusCodes.Status200OK);

api.MapDelete("/body-reference-photos/{photoId:guid}", (Guid photoId, WardrobeService wardrobe, HttpContext context) =>
    wardrobe.DeleteBodyReferencePhoto(CurrentUser(context), photoId) ? Results.NoContent() : Results.NotFound());

api.MapGet("/garments", (
    WardrobeService wardrobe,
    HttpContext context,
    GarmentCategory? category,
    string? color,
    string? season,
    string? q,
    string? sort,
    int? offset,
    int? limit,
    bool? favorite,
    bool? archived,
    string? occasion,
    string? brand,
    string? material) =>
    Results.Ok(wardrobe.ListGarments(CurrentUser(context), new GarmentQuery(
        category,
        color,
        season,
        q,
        sort,
        offset,
        limit,
        favorite,
        archived,
        occasion,
        brand,
        material))))
    .Produces<IReadOnlyList<GarmentItem>>(StatusCodes.Status200OK);

api.MapGet("/garments/{garmentId:guid}", (Guid garmentId, WardrobeService wardrobe, HttpContext context) =>
{
    var garment = wardrobe.GetGarment(CurrentUser(context), garmentId);
    return garment is null ? Results.NotFound() : Results.Ok(garment);
})
    .Produces<GarmentItem>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status404NotFound);

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
            request.Tags ?? Array.Empty<string>(),
            request.PrimaryColor,
            request.SecondaryColors,
            request.Material,
            request.Brand,
            request.Size,
            request.Season,
            request.WeatherMinTemp,
            request.WeatherMaxTemp,
            request.Occasion,
            request.FormalityScore,
            request.WarmthScore,
            request.ComfortScore,
            request.IsFavorite ?? false,
            request.IsArchived ?? false,
            request.LastWornAt,
            request.LaundryStatus));

        return Results.Created($"/api/garments/{garment.Id}", garment);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
    .Produces<GarmentItem>(StatusCodes.Status201Created);

api.MapPatch("/garments/{garmentId:guid}", (Guid garmentId, UpdateGarmentRequest request, WardrobeService wardrobe, HttpContext context) =>
{
    try
    {
        var garment = wardrobe.UpdateGarment(CurrentUser(context), garmentId, new UpdateGarmentCommand(
            request.Name,
            request.Category,
            request.Tags,
            request.PrimaryColor,
            request.SecondaryColors,
            request.Material,
            request.Brand,
            request.Size,
            request.Season,
            request.WeatherMinTemp,
            request.WeatherMaxTemp,
            request.Occasion,
            request.FormalityScore,
            request.WarmthScore,
            request.ComfortScore,
            request.IsFavorite,
            request.IsArchived,
            request.LastWornAt,
            request.LaundryStatus));
        return garment is null ? Results.NotFound() : Results.Ok(garment);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
    .Produces<GarmentItem>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status404NotFound);

api.MapDelete("/garments/{garmentId:guid}", (Guid garmentId, WardrobeService wardrobe, HttpContext context) =>
    wardrobe.DeleteGarment(CurrentUser(context), garmentId) ? Results.NoContent() : Results.NotFound());

api.MapGet("/outfits", (
    OutfitService outfits,
    HttpContext context,
    string? q,
    string? occasion,
    bool? favorite,
    bool? archived,
    string? sort,
    int? offset,
    int? limit) =>
    Results.Ok(outfits.ListOutfits(CurrentUser(context), new OutfitQuery(q, occasion, favorite, archived, sort, offset, limit))))
    .Produces<IReadOnlyList<Outfit>>(StatusCodes.Status200OK);

api.MapGet("/outfits/{outfitId:guid}", (Guid outfitId, OutfitService outfits, HttpContext context) =>
{
    var outfit = outfits.GetOutfit(CurrentUser(context), outfitId);
    return outfit is null ? Results.NotFound() : Results.Ok(outfit);
})
    .Produces<Outfit>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status404NotFound);

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
})
    .Produces<Outfit>(StatusCodes.Status201Created);

api.MapPatch("/outfits/{outfitId:guid}", (Guid outfitId, UpdateOutfitRequest request, OutfitService outfits, HttpContext context) =>
{
    try
    {
        var outfit = outfits.UpdateOutfit(CurrentUser(context), outfitId, new UpdateOutfitCommand(
            request.Name,
            request.GarmentIds,
            request.Tags,
            request.Occasion,
            request.IsFavorite,
            request.IsArchived));
        return outfit is null ? Results.NotFound() : Results.Ok(outfit);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
    .Produces<Outfit>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status404NotFound);

api.MapDelete("/outfits/{outfitId:guid}", (Guid outfitId, OutfitService outfits, HttpContext context) =>
    outfits.DeleteOutfit(CurrentUser(context), outfitId) ? Results.NoContent() : Results.NotFound());

api.MapPost("/outfits/{outfitId:guid}/try-on", async (
    Guid outfitId,
    StartTryOnRequest request,
    TryOnService tryOn,
    HttpContext context,
    CancellationToken cancellationToken) =>
{
    try
    {
        var job = await tryOn.StartAsync(
            CurrentUser(context),
            outfitId,
            request.BodyReferencePhotoUrl,
            request.ConsentAccepted,
            request.SequentialFlowEnabled,
            request.BodyReferencePhotoId,
            cancellationToken);
        return Results.Accepted($"/api/try-on-jobs/{job.Id}", job);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
    .Produces<TryOnJob>(StatusCodes.Status202Accepted);

api.MapGet("/try-on-jobs/{jobId:guid}", (Guid jobId, TryOnService tryOn, HttpContext context) =>
{
    var job = tryOn.GetJob(CurrentUser(context), jobId);
    return job is null ? Results.NotFound() : Results.Ok(job);
})
    .Produces<TryOnJob>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status404NotFound);

api.MapDelete("/try-on-jobs/{jobId:guid}/output", (Guid jobId, TryOnService tryOn, HttpContext context) =>
    tryOn.DeleteOutput(CurrentUser(context), jobId) ? Results.NoContent() : Results.NotFound());

api.MapPost("/privacy/purge-ai-outputs", (TryOnService tryOn, HttpContext context) =>
    Results.Ok(new { purged = tryOn.PurgeAiOutputs(CurrentUser(context)) }));

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
})
    .Produces<ScheduledOutfit>(StatusCodes.Status200OK);

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
})
    .Produces<IReadOnlyList<ScheduledOutfit>>(StatusCodes.Status200OK);

api.MapDelete("/schedule/{date}", (string date, ScheduleService schedule, HttpContext context) =>
{
    if (!DateOnly.TryParse(date, out var scheduledDate))
    {
        return Results.BadRequest(new { error = "Route parameter 'date' must be an ISO date." });
    }

    return schedule.UnscheduleOutfit(CurrentUser(context), scheduledDate) ? Results.NoContent() : Results.NotFound();
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
    return outfit is null
        ? Results.NotFound()
        : Results.Ok(new SharedOutfitResponse(
            outfit.Id,
            outfit.Name,
            outfit.Items,
            outfit.Tags,
            outfit.Occasion,
            outfit.IsFavorite,
            outfit.IsArchived,
            outfit.ClothesOnlyPreviewUrl,
            outfit.PersonPreviewUrl,
            outfit.CreatedAt));
})
    .Produces<SharedOutfitResponse>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status404NotFound);

api.MapDelete("/share/{token}", (string token, ShareService share, HttpContext context) =>
    share.RevokeShareLink(CurrentUser(context), token) ? Results.NoContent() : Results.NotFound());

app.MapGet("/uploads/garments/{fileName}", (string fileName, IStoredPhotoReader photos) =>
{
    var photo = photos.GetGarmentPhoto(fileName);
    return photo is null ? Results.NotFound() : Results.File(photo.FullPath, photo.ContentType);
});

app.MapGet("/uploads/body-reference-photos/{fileName}", (string fileName) => Results.NotFound());

app.MapGet("/api/storage/signed/{**objectKey}", (
    string objectKey,
    long expires,
    string signature,
    IObjectStorage objects,
    IClock clock) =>
{
    if (objects is not LocalObjectStorage local)
    {
        return Results.NotFound();
    }

    var stored = local.GetSignedObject(objectKey, expires, signature, clock.UtcNow);
    return stored is null ? Results.NotFound() : Results.File(stored.FullPath, stored.ContentType);
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

static ITryOnProvider CreateTryOnProvider(IServiceProvider provider, IConfiguration configuration)
{
    var configuredProvider = configuration["TryOn:Provider"] ?? "Mock";
    var httpFactory = provider.GetRequiredService<IHttpClientFactory>();

    return configuredProvider.Trim().ToLowerInvariant() switch
    {
        "fashn" => new FashnTryOnProvider(
            httpFactory.CreateClient("fashn"),
            new FashnTryOnSettings(
                configuration["Fashn:ApiKey"] ?? "",
                configuration["Fashn:ModelName"] ?? "tryon-v1.6",
                configuration["Fashn:Mode"] ?? "balanced",
                configuration.GetValue("Fashn:MaxPollingAttempts", 30),
                TimeSpan.FromSeconds(configuration.GetValue("Fashn:PollIntervalSeconds", 2)))),
        "localvton" or "local-vton" => new LocalVtonProvider(
            httpFactory.CreateClient("local-vton"),
            HttpProviderSettings(configuration, "LocalVton", "http://localhost:7860/", "/try-on", requiresApiKey: false)),
        "localcatvton" or "local-cat-vton" or "localcatvtonprovider" => new LocalCatVtonProvider(
            httpFactory.CreateClient("local-cat-vton"),
            HttpProviderSettings(configuration, "LocalCatVton", "http://localhost:7861/", "/try-on", requiresApiKey: false)),
        "replicate" => new ReplicateProvider(
            httpFactory.CreateClient("replicate"),
            HttpProviderSettings(configuration, "Replicate", "https://api.replicate.com/v1/", "/predictions", requiresApiKey: true)),
        "fal" => new FalProvider(
            httpFactory.CreateClient("fal"),
            HttpProviderSettings(configuration, "Fal", "https://fal.run/", "/try-on", requiresApiKey: true)),
        _ => new MockTryOnProvider()
    };
}

static IObjectStorage CreateObjectStorage(IConfiguration configuration, IWebHostEnvironment environment)
{
    var provider = (configuration["ObjectStorage:Provider"] ?? "Local").Trim().ToLowerInvariant();
    if (provider is "s3" or "minio")
    {
        return new MinioObjectStorage(new MinioObjectStorageSettings(
            configuration["ObjectStorage:S3:Endpoint"] ?? configuration["Minio:Endpoint"] ?? "",
            configuration["ObjectStorage:S3:AccessKey"] ?? configuration["Minio:AccessKey"] ?? "",
            configuration["ObjectStorage:S3:SecretKey"] ?? configuration["Minio:SecretKey"] ?? "",
            configuration["ObjectStorage:S3:Bucket"] ?? configuration["Minio:Bucket"] ?? "outfit-planner-private",
            ForcePathStyle: configuration.GetValue("ObjectStorage:S3:ForcePathStyle", true),
            Region: configuration["ObjectStorage:S3:Region"] ?? "us-east-1"));
    }

    var root = configuration["ObjectStorage:Local:Root"]
        ?? Path.Combine(environment.ContentRootPath, "storage", "objects");
    return new LocalObjectStorage(root, configuration["ObjectStorage:Local:SigningSecret"]);
}

static HttpTryOnProviderSettings HttpProviderSettings(
    IConfiguration configuration,
    string providerName,
    string defaultBaseUrl,
    string defaultEndpoint,
    bool requiresApiKey)
{
    return new HttpTryOnProviderSettings(
        ProviderSetting(configuration, providerName, "BaseUrl", defaultBaseUrl),
        ProviderSetting(configuration, providerName, "Endpoint", defaultEndpoint),
        ProviderSetting(configuration, providerName, "ApiKey", ""),
        ProviderSetting(configuration, providerName, "ModelName", providerName),
        requiresApiKey);
}

static string ProviderSetting(IConfiguration configuration, string providerName, string key, string fallback)
{
    return configuration[$"TryOn:{providerName}:{key}"]
        ?? configuration[$"{providerName}:{key}"]
        ?? fallback;
}

static string CurrentUser(HttpContext context)
{
    return context.Items.TryGetValue(CurrentUserItemKey, out var userId) && userId is string value
        ? value
        : throw new InvalidOperationException("Authenticated user was not resolved for this request.");
}

static bool IsOpenApiDocumentGeneration()
{
    return Environment.CommandLine.Contains("dotnet-getdocument", StringComparison.OrdinalIgnoreCase)
        || Environment.CommandLine.Contains("GetDocument.Insider", StringComparison.OrdinalIgnoreCase);
}

static bool RequiresAuthenticatedUser(HttpContext context)
{
    if (!context.Request.Path.StartsWithSegments("/api", out var remaining))
    {
        return false;
    }

    var path = remaining.Value ?? "";
    return !path.Equals("/health", StringComparison.OrdinalIgnoreCase)
        && !path.Equals("/system/status", StringComparison.OrdinalIgnoreCase)
        && !path.StartsWith("/auth/", StringComparison.OrdinalIgnoreCase)
        && !path.StartsWith("/openapi/", StringComparison.OrdinalIgnoreCase)
        && !path.StartsWith("/storage/signed/", StringComparison.OrdinalIgnoreCase)
        && !(HttpMethods.IsGet(context.Request.Method) && path.StartsWith("/share/", StringComparison.OrdinalIgnoreCase));
}

static bool RequiresCsrfToken(HttpRequest request)
{
    return !HttpMethods.IsGet(request.Method)
        && !HttpMethods.IsHead(request.Method)
        && !HttpMethods.IsOptions(request.Method);
}

static void IssueAuthCookies(HttpContext context, AuthResult result, IWebHostEnvironment environment)
{
    context.Response.Cookies.Append(SessionCookieName, result.SessionToken, SessionCookieOptions(result.ExpiresAt, environment, context.Request.IsHttps));
    context.Response.Cookies.Append(CsrfCookieName, result.CsrfToken, CsrfCookieOptions(result.ExpiresAt, environment, context.Request.IsHttps));
}

static void ClearAuthCookies(HttpContext context, IWebHostEnvironment environment)
{
    context.Response.Cookies.Delete(SessionCookieName, SessionCookieOptions(DateTimeOffset.UnixEpoch, environment, context.Request.IsHttps));
    context.Response.Cookies.Delete(CsrfCookieName, CsrfCookieOptions(DateTimeOffset.UnixEpoch, environment, context.Request.IsHttps));
}

static CookieOptions SessionCookieOptions(DateTimeOffset expiresAt, IWebHostEnvironment environment, bool isHttps)
{
    return new CookieOptions
    {
        HttpOnly = true,
        Secure = !environment.IsDevelopment() || isHttps,
        SameSite = SameSiteMode.Lax,
        Expires = expiresAt,
        Path = "/"
    };
}

static CookieOptions CsrfCookieOptions(DateTimeOffset expiresAt, IWebHostEnvironment environment, bool isHttps)
{
    return new CookieOptions
    {
        HttpOnly = false,
        Secure = !environment.IsDevelopment() || isHttps,
        SameSite = SameSiteMode.Lax,
        Expires = expiresAt,
        Path = "/"
    };
}

static AuthSessionResponse ToAuthSessionResponse(AuthResult result)
{
    return new AuthSessionResponse(ToAuthUserResponse(result.User), result.ExpiresAt);
}

static AuthSessionResponse ToAuthSessionResponseFromSession(AuthenticatedSession session)
{
    return new AuthSessionResponse(ToAuthUserResponse(session.User), session.ExpiresAt);
}

static AuthUserResponse ToAuthUserResponse(PublicUser user)
{
    return new AuthUserResponse(user.Id, user.Email, user.DisplayName);
}

static bool TryNormalizeExternalProvider(string provider, out string normalizedProvider)
{
    normalizedProvider = provider.Trim().ToLowerInvariant();
    if (normalizedProvider is not ("google" or "apple"))
    {
        return false;
    }

    return true;
}

static bool IsExternalProviderConfigured(string provider, bool googleConfigured, bool appleConfigured)
{
    return provider switch
    {
        "google" => googleConfigured,
        "apple" => appleConfigured,
        _ => false
    };
}

static string NormalizeReturnUrl(string? returnUrl)
{
    if (string.IsNullOrWhiteSpace(returnUrl))
    {
        return "/";
    }

    return returnUrl.StartsWith('/') && !returnUrl.StartsWith("//", StringComparison.Ordinal)
        ? returnUrl
        : "/";
}

static bool IsExternalEmailVerified(ClaimsPrincipal principal)
{
    var claim = principal.FindFirst("email_verified")?.Value;
    return string.Equals(claim, "true", StringComparison.OrdinalIgnoreCase) || claim == "1";
}

static string BuildOAuthAuthorizationRedirectUri(RedirectContext<OAuthOptions> context, string? publicOrigin)
{
    if (publicOrigin is null)
    {
        return context.RedirectUri;
    }

    var authorizationEndpoint = new Uri(context.RedirectUri);
    var query = QueryHelpers.ParseQuery(authorizationEndpoint.Query)
        .ToDictionary(
            pair => pair.Key,
            pair => (string?)pair.Value.ToString(),
            StringComparer.Ordinal);
    query["redirect_uri"] = BuildExternalCallbackUri(publicOrigin, context.Options.CallbackPath);

    return QueryHelpers.AddQueryString(authorizationEndpoint.GetLeftPart(UriPartial.Path), query);
}

static string? NormalizePublicOrigin(string? origin)
{
    if (string.IsNullOrWhiteSpace(origin))
    {
        return null;
    }

    if (!Uri.TryCreate(origin.Trim(), UriKind.Absolute, out var uri)
        || uri.Scheme is not ("http" or "https")
        || string.IsNullOrWhiteSpace(uri.Host))
    {
        throw new InvalidOperationException("Authentication:PublicOrigin must be an absolute HTTP or HTTPS origin.");
    }

    return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
}

static string BuildExternalCallbackUri(string publicOrigin, PathString callbackPath)
{
    return $"{publicOrigin}{callbackPath}";
}

public partial class Program;
