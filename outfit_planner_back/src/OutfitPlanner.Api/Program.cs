using OutfitPlanner.Application.Abstractions;
using OutfitPlanner.Application.Services;
using OutfitPlanner.Api;
using OutfitPlanner.Api.Authentication;
using OutfitPlanner.Api.Contracts;
using OutfitPlanner.Domain;
using OutfitPlanner.Infrastructure.AutoTagging;
using OutfitPlanner.Infrastructure.Diagnostics;
using OutfitPlanner.Infrastructure.Security;
using OutfitPlanner.Infrastructure.Storage;
using OutfitPlanner.Infrastructure.TryOn;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.DataProtection;
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
const string CurrentUserRoleItemKey = "outfit.current_user_role";
const string CsrfHeaderName = "X-CSRF-Token";

LoadDotEnvConfigurationAliases(builder.Configuration, builder.Environment.ContentRootPath, args);

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

// Development always allows the local Vite/preview origins. Outside Development only origins that
// are explicitly configured (Cors:AllowedOrigins, comma-separated) are allowed, so a same-origin
// deploy needs no config and a split-origin deploy must opt its frontend origin in (rather than
// silently leaving localhost allowed in production).
var developmentCorsOrigins = new[]
{
    "http://localhost:5173",
    "http://127.0.0.1:5173",
    "http://localhost:4173",
    "http://127.0.0.1:4173",
    "https://localhost:5173",
    "https://127.0.0.1:5173",
    "https://localhost:4173",
    "https://127.0.0.1:4173"
};
var configuredCorsOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? string.Empty)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Select(origin => origin.TrimEnd('/'));
var allowedCorsOrigins = (builder.Environment.IsDevelopment() ? developmentCorsOrigins : Array.Empty<string>())
    .Concat(configuredCorsOrigins)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy
            .WithOrigins(allowedCorsOrigins)
            .AllowCredentials()
            .AllowAnyHeader()
            .AllowAnyMethod());
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (rejection, cancellationToken) =>
    {
        if (rejection.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            rejection.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
        }

        rejection.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await rejection.HttpContext.Response.WriteAsJsonAsync(new { error = "Too many requests. Please retry later." }, cancellationToken);
    };

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
    // Throttle paid AI try-on work per session (falls back to IP for unauthenticated callers),
    // since the rate limiter runs before the user id is resolved in the pipeline.
    options.AddPolicy("try-on-rate-limit", context =>
    {
        var sessionToken = context.Request.Cookies[SessionCookieName];
        // Partition on a non-reversible digest of the session token, never the raw bearer
        // secret, so the token cannot surface in rate-limiter diagnostics/metrics.
        var partitionKey = !string.IsNullOrEmpty(sessionToken)
            ? $"session:{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(sessionToken)))}"
            : $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30,
            Window = TimeSpan.FromMinutes(5),
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        });
    });
});

builder.Services.AddSingleton<IClock, OutfitPlanner.Infrastructure.Security.SystemClock>();
builder.Services.AddSingleton<IShareTokenGenerator, SecureShareTokenGenerator>();
builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddSingleton<IAuthTokenService, SecureAuthTokenService>();
// Role pins ("always admin/premium" accounts) resolve by normalized email at read time; the
// defaults apply when the Roles__Pinned*Emails settings are unset.
builder.Services.AddSingleton(new RolePinningPolicy(LoadRolePinningOptions(builder.Configuration)));
// Paywall tiers: one catalog drives caps, allowed AI modes, resolution, and credit
// allowances (numbers overridable via Paywall__Free__*/Paywall__Premium__*).
builder.Services.AddSingleton(LoadPlanCatalog(builder.Configuration));
builder.Services.AddSingleton<CreditLedgerService>();
builder.Services.AddSingleton<EntitlementService>();

// Stage-4 billing (PAYWALL_MODEL.md): Stripe when a secret key is configured, disabled
// otherwise (Billing__Provider=Auto|Stripe|Disabled). Numbers/prices ride Stripe__*.
builder.Services.AddSingleton(LoadBillingOptions(builder.Configuration));
builder.Services.AddSingleton<IBillingProvider>(_ => CreateBillingProvider(builder.Configuration));
builder.Services.AddSingleton<BillingService>();
var authenticationBuilder = builder.Services.AddAuthentication();
var externalAuthPublicOrigin = NormalizePublicOrigin(builder.Configuration["Authentication:PublicOrigin"]);
var publicOrigin = NormalizePublicOrigin(builder.Configuration["PublicOrigin"]) ?? externalAuthPublicOrigin;
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
builder.Services.AddHttpClient("composite-fashn");
builder.Services.AddHttpClient("self-hosted-catvton");
builder.Services.AddHttpClient("general-image-edit");
builder.Services.AddHttpClient("background-removal");
builder.Services.AddHttpClient("auto-tagging");
builder.Services.AddHttpClient("try-on-output-storage");
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

if (string.IsNullOrWhiteSpace(redisConnectionString))
{
    builder.Services.AddSingleton<IBackgroundRemovalJobQueue, OutfitPlanner.Infrastructure.BackgroundRemoval.InMemoryBackgroundRemovalJobQueue>();
}
else
{
    builder.Services.AddSingleton<IBackgroundRemovalJobQueue>(provider => new OutfitPlanner.Infrastructure.BackgroundRemoval.RedisBackgroundRemovalJobQueue(provider.GetRequiredService<IConnectionMultiplexer>()));
}
// Persist DataProtection keys (used to encrypt OAuth correlation/state cookies) so they survive
// container restarts — otherwise external logins fail with "Correlation failed" after a restart.
// Stored under the api_storage volume. Skipped during build-time OpenAPI generation.
if (!IsOpenApiDocumentGeneration())
{
    var dataProtectionKeysPath = builder.Configuration["DataProtection:KeyRingPath"]
        ?? Path.Combine(builder.Environment.ContentRootPath, "storage", "dataprotection-keys");
    Directory.CreateDirectory(dataProtectionKeysPath);
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
        .SetApplicationName("outfit-planner");
}

EnsureLocalObjectStorageSigningSecret(builder.Configuration, builder.Environment);
EnsureProviderConfiguration(builder.Configuration);
builder.Services.AddSingleton<IObjectStorage>(provider => CreateObjectStorage(builder.Configuration, builder.Environment, publicOrigin));
builder.Services.AddSingleton<IStoredPhotoUrlRefresher>(provider => new StoredPhotoUrlRefresher(
    provider.GetRequiredService<IObjectStorage>(),
    publicOrigin));
builder.Services.AddSingleton<ITryOnOutputStorage>(provider => new TryOnOutputStorage(
    provider.GetRequiredService<IObjectStorage>(),
    provider.GetRequiredService<IHttpClientFactory>().CreateClient("try-on-output-storage")));
builder.Services.AddSingleton<IBackgroundRemovalProvider>(provider => CreateBackgroundRemovalProvider(provider, builder.Configuration));
builder.Services.AddSingleton<IGarmentExtractionProvider>(provider => new SingleGarmentExtractionProvider(provider.GetRequiredService<IBackgroundRemovalProvider>()));
builder.Services.AddSingleton<IImageProcessor>(provider => new ImageProcessor(provider.GetRequiredService<IGarmentExtractionProvider>()));
builder.Services.AddSingleton<LocalPhotoStorage>();
builder.Services.AddSingleton<IPhotoStorage>(provider => provider.GetRequiredService<LocalPhotoStorage>());
builder.Services.AddSingleton<IStoredPhotoReader>(provider => provider.GetRequiredService<LocalPhotoStorage>());
builder.Services.AddSingleton<IStoredPhotoDeletion>(provider => provider.GetRequiredService<LocalPhotoStorage>());
builder.Services.AddSingleton<IGarmentImageRotator>(provider => provider.GetRequiredService<LocalPhotoStorage>());
builder.Services.AddSingleton<IGarmentOriginalImageReader>(provider => provider.GetRequiredService<LocalPhotoStorage>());
builder.Services.AddSingleton<IGarmentCutoutImageReader>(provider => provider.GetRequiredService<LocalPhotoStorage>());
builder.Services.AddSingleton<IGarmentBackgroundRemover>(provider => provider.GetRequiredService<LocalPhotoStorage>());
// Garment auto-tagging (prefill suggestions). Cutout factory reuses the existing extraction
// pipeline; the tagger is provider-selected and degrades to no-op when the local service is off.
builder.Services.AddSingleton<IGarmentCutoutFactory>(provider => new GarmentCutoutFactory(provider.GetRequiredService<IGarmentExtractionProvider>()));
builder.Services.AddSingleton<IGarmentAutoTagger>(provider => CreateGarmentAutoTagger(provider, builder.Configuration));
builder.Services.AddSingleton<GarmentAutoTagService>();
builder.Services.AddSingleton<IBackgroundRemovalJobRepository, OutfitPlanner.Infrastructure.BackgroundRemoval.InMemoryBackgroundRemovalJobRepository>();
builder.Services.AddSingleton<IBackgroundRemovalJobProcessor, BackgroundRemovalJobProcessor>();
// Global hairstyle presets vendored under assets/hairstyles (copied to the output directory at
// build time, like database/); manifest.json is the catalog's source of truth.
builder.Services.AddSingleton<IHairstylePresetCatalog>(new ManifestHairstylePresetCatalog(
    Path.Combine(AppContext.BaseDirectory, "assets", "hairstyles")));
var postgresConnectionString = builder.Configuration.GetConnectionString("Postgres");
var storageProvider = string.IsNullOrWhiteSpace(postgresConnectionString) ? "LocalFile" : "Postgres";
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
    builder.Services.AddSingleton<IAdminUserRepository>(provider => provider.GetRequiredService<PostgresOutfitStore>());
    builder.Services.AddSingleton<ICreditLedgerRepository>(provider => provider.GetRequiredService<PostgresOutfitStore>());
    builder.Services.AddSingleton<ISubscriptionRepository>(provider => provider.GetRequiredService<PostgresOutfitStore>());
    builder.Services.AddSingleton<IBillingEventRepository>(provider => provider.GetRequiredService<PostgresOutfitStore>());
}
else
{
    builder.Services.AddSingleton(provider => CreateLocalOutfitStore(builder.Configuration, builder.Environment));
    builder.Services.AddSingleton<IBodyReferencePhotoRepository>(provider => provider.GetRequiredService<FileBackedOutfitStore>());
    builder.Services.AddSingleton<IGarmentRepository>(provider => provider.GetRequiredService<FileBackedOutfitStore>());
    builder.Services.AddSingleton<IOutfitRepository>(provider => provider.GetRequiredService<FileBackedOutfitStore>());
    builder.Services.AddSingleton<IOutfitScheduleRepository>(provider => provider.GetRequiredService<FileBackedOutfitStore>());
    builder.Services.AddSingleton<ITryOnJobRepository>(provider => provider.GetRequiredService<FileBackedOutfitStore>());
    builder.Services.AddSingleton<IShareLinkRepository>(provider => provider.GetRequiredService<FileBackedOutfitStore>());
    builder.Services.AddSingleton<IUserAccountRepository>(provider => provider.GetRequiredService<FileBackedOutfitStore>());
    builder.Services.AddSingleton<IAdminUserRepository>(provider => provider.GetRequiredService<FileBackedOutfitStore>());
    builder.Services.AddSingleton<ICreditLedgerRepository>(provider => provider.GetRequiredService<FileBackedOutfitStore>());
    builder.Services.AddSingleton<ISubscriptionRepository>(provider => provider.GetRequiredService<FileBackedOutfitStore>());
    builder.Services.AddSingleton<IBillingEventRepository>(provider => provider.GetRequiredService<FileBackedOutfitStore>());
}
builder.Services.AddSingleton<WardrobeService>();
builder.Services.AddSingleton<PhotoUploadService>();
builder.Services.AddSingleton<OutfitService>();
builder.Services.AddSingleton<ScheduleService>();
builder.Services.AddSingleton<TryOnCostEstimator>();
builder.Services.AddSingleton<TryOnService>();
builder.Services.AddSingleton<ShareService>();
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<AdminService>();
builder.Services.AddSingleton<PostgresConnectionProbe>();
builder.Services.AddHostedService<TryOnBackgroundWorker>();
builder.Services.AddHostedService<GarmentPerceptualHashBackfillWorker>();
builder.Services.AddHostedService<GarmentCutoutMeasurementBackfillWorker>();
builder.Services.AddHostedService<BackgroundRemovalWorker>();

var app = builder.Build();

if (!IsOpenApiDocumentGeneration())
{
    app.Services.GetService<PostgresMigrationRunner>()?.Initialize();
}

var detailedErrorsEnabled = app.Environment.IsDevelopment()
    || app.Environment.IsEnvironment("Test")
    || builder.Configuration.GetValue("DetailedErrors", false);

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.Use(async (context, next) =>
{
    var traceId = context.TraceIdentifier;
    context.Response.Headers["X-Trace-Id"] = traceId;
    // Baseline security headers for every response, including served image bytes and signed URLs.
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";

    try
    {
        await next(context);
    }
    catch (Exception ex)
    {
        // Validation failures are user input errors, not server faults: map to 400 with the message
        // (safe to surface) and do not log them as errors. Everything else is a 500.
        if (ex is OutfitPlanner.Domain.ValidationException validationFailure)
        {
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await Results.Json(new { error = validationFailure.Message, traceId }).ExecuteAsync(context);
            }

            return;
        }

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
    context.Items[CurrentUserRoleItemKey] = session.User.Role;
    await next(context);
});

var api = app.MapGroup("/api");
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Test"))
{
    app.MapOpenApi("/api/openapi/{documentName}.json");
}

api.MapGet("/health", () => Results.Ok(new { status = "ok", service = "outfit-planner-api" }));

api.MapGet("/system/status", async (PostgresConnectionProbe postgres, IBackgroundRemovalProvider backgroundRemoval, IGarmentAutoTagger autoTagger, CancellationToken cancellationToken) =>
{
    var postgresStatus = await postgres.CheckAsync(cancellationToken);
    return Results.Ok(new
    {
        api = "running",
        storage = storageProvider,
        postgres = postgresStatus,
        aiProvider = builder.Configuration["TryOn:Provider"] ?? "Mock",
        backgroundRemovalProvider = backgroundRemoval.Name,
        backgroundRemovalConfiguredProvider = builder.Configuration["BackgroundRemoval:Provider"] ?? "Auto",
        autoTaggingProvider = autoTagger.Name,
        autoTaggingConfiguredProvider = builder.Configuration["AutoTagging:Provider"] ?? "Auto"
    });
});

api.MapGet("/auth/providers", () => Results.Ok(new[]
{
    new AuthProviderResponse("email", "Email", true, "password"),
    new AuthProviderResponse("google", "Google", googleConfigured, "oauth"),
    new AuthProviderResponse("apple", "Apple", appleConfigured, "oidc")
}));

api.MapPost("/auth/register", (RegisterRequest request, AuthService auth, IStoredPhotoUrlRefresher photoUrls, HttpContext context) =>
{
    try
    {
        var result = auth.RegisterWithPassword(request.Email, request.Password, request.RepeatPassword);
        IssueAuthCookies(context, result, app.Environment);
        return Results.Ok(ToAuthSessionResponse(result, photoUrls, context.Request));
    }
    catch (ValidationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).RequireRateLimiting("registration-rate-limit");

api.MapPost("/auth/login", (LoginRequest request, AuthService auth, IStoredPhotoUrlRefresher photoUrls, HttpContext context) =>
{
    try
    {
        var result = auth.SignInWithPassword(request.Email, request.Password);
        IssueAuthCookies(context, result, app.Environment);
        return Results.Ok(ToAuthSessionResponse(result, photoUrls, context.Request));
    }
    catch (ValidationException ex)
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

api.MapGet("/auth/me", (AuthService auth, IStoredPhotoUrlRefresher photoUrls, HttpContext context) =>
{
    var session = auth.AuthenticateSession(context.Request.Cookies[SessionCookieName], null, requireCsrf: false);
    return session is null ? Results.Unauthorized() : Results.Ok(ToAuthSessionResponseFromSession(session, photoUrls, context.Request));
});

api.MapPost("/auth/email-verification/request", (EmailVerificationRequest request, AuthService auth) =>
{
    try
    {
        var token = auth.CreateEmailVerificationToken(request.Email);
        return Results.Ok(new { status = "verification-requested", token = app.Environment.IsDevelopment() ? token : null });
    }
    catch (ValidationException)
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
    catch (ValidationException)
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
    catch (ValidationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
}).RequireRateLimiting("login-rate-limit");

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
    var account = users.GetUserById(userId);
    return Results.Ok(new
    {
        // Sanitized profile: the password hash, normalized email, and avatar object key are
        // server-internal and must never leave the API, even in the owner's own export.
        user = account is null ? null : new
        {
            account.Id,
            account.Email,
            account.DisplayName,
            account.Gender,
            account.Role,
            account.CreatedAt,
            account.UpdatedAt,
            account.LastLoginAt,
            account.EmailVerifiedAt,
            account.AvatarUrl
        },
        garments = garments.ListGarmentsByUser(userId),
        outfits = outfits.ListOutfitsByUser(userId),
        bodyReferencePhotos = bodyPhotos.ListBodyReferencePhotosByUser(userId),
        tryOnJobs = tryOnJobs.ListTryOnJobsByUser(userId)
    });
});

api.MapGet("/account/entitlements", (EntitlementService entitlements, HttpContext context) =>
{
    var account = entitlements.Get(CurrentUser(context));
    return Results.Ok(new AccountEntitlementsResponse(
        account.Role,
        account.Limits.MaxGarments,
        account.Limits.MaxOutfits,
        account.Limits.MaxBodyReferencePhotos,
        account.GarmentCount,
        account.OutfitCount,
        account.BodyReferencePhotoCount,
        account.Credits.Unlimited,
        account.Credits.Balance,
        account.Credits.MonthlyAllowance,
        account.Limits.AllowedAiModes,
        account.Limits.MaxTryOnResolution,
        account.Limits.PriorityQueue));
})
    .Produces<AccountEntitlementsResponse>(StatusCodes.Status200OK);

api.MapGet("/billing", (BillingService billing, HttpContext context) =>
    Results.Ok(ToBillingStatusResponse(billing.GetStatus(CurrentUser(context)))))
    .Produces<BillingStatusResponse>(StatusCodes.Status200OK);

api.MapPost("/billing/checkout", async (BillingService billing, HttpContext context, CancellationToken cancellationToken) =>
{
    try
    {
        var url = await billing.StartSubscriptionCheckoutAsync(CurrentUser(context), cancellationToken);
        return Results.Ok(new BillingCheckoutResponse(url));
    }
    catch (ValidationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
    .Produces<BillingCheckoutResponse>(StatusCodes.Status200OK);

api.MapPost("/billing/topup", async (StartTopUpCheckoutRequest request, BillingService billing, HttpContext context, CancellationToken cancellationToken) =>
{
    try
    {
        var url = await billing.StartTopUpCheckoutAsync(CurrentUser(context), request.PackId, cancellationToken);
        return Results.Ok(new BillingCheckoutResponse(url));
    }
    catch (ValidationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
    .Produces<BillingCheckoutResponse>(StatusCodes.Status200OK);

api.MapPost("/billing/portal", async (BillingService billing, HttpContext context, CancellationToken cancellationToken) =>
{
    try
    {
        var url = await billing.CreatePortalAsync(CurrentUser(context), cancellationToken);
        return Results.Ok(new BillingCheckoutResponse(url));
    }
    catch (ValidationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
    .Produces<BillingCheckoutResponse>(StatusCodes.Status200OK);

// Anonymous by design (allowlisted in RequiresAuthenticatedUser): Stripe calls this
// without cookies, and the signature check is the authentication.
api.MapPost("/billing/webhook", async (HttpRequest request, BillingService billing, CancellationToken cancellationToken) =>
{
    using var reader = new StreamReader(request.Body);
    var payload = await reader.ReadToEndAsync(cancellationToken);
    try
    {
        var result = await billing.HandleWebhookAsync(payload, request.Headers["Stripe-Signature"], cancellationToken);
        return Results.Ok(new { status = result.Status });
    }
    catch (ValidationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

api.MapPatch("/account/profile", (
    UpdateAccountProfileRequest request,
    AuthService auth,
    IStoredPhotoUrlRefresher photoUrls,
    HttpContext context) =>
{
    try
    {
        auth.UpdateProfile(CurrentUser(context), request.Username, request.Gender);
        var session = auth.AuthenticateSession(context.Request.Cookies[SessionCookieName], null, requireCsrf: false)
            ?? throw new InvalidOperationException("Authentication is required.");
        return Results.Ok(ToAuthSessionResponseFromSession(session, photoUrls, context.Request));
    }
    catch (ValidationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
    .Produces<AuthSessionResponse>(StatusCodes.Status200OK);

api.MapPost("/account/avatar", async (
    HttpRequest request,
    AuthService auth,
    PhotoUploadService photos,
    IStoredPhotoUrlRefresher photoUrls,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    try
    {
        var stored = await StoreMultipartPhoto(request, logger, "avatar", cancellationToken, photo => photos.UploadAvatarPhoto(photo));
        var publicUrl = PublicUploadUrl(request, stored.Url)
            ?? throw new InvalidOperationException("Stored avatar URL is required.");
        auth.UpdateAvatar(CurrentUser(request.HttpContext), publicUrl, stored.ThumbnailObjectKey ?? stored.ObjectKey);
        var session = auth.AuthenticateSession(request.HttpContext.Request.Cookies[SessionCookieName], null, requireCsrf: false)
            ?? throw new InvalidOperationException("Authentication is required.");
        return Results.Ok(ToAuthSessionResponseFromSession(session, photoUrls, request));
    }
    catch (ValidationException ex)
    {
        logger.LogWarning(ex, "Upload diagnostics: rejected avatar upload trace {TraceId}", request.HttpContext.TraceIdentifier);
        return Results.BadRequest(new { error = ex.Message });
    }
})
    .Produces<AuthSessionResponse>(StatusCodes.Status200OK);

api.MapDelete("/account", (
    IUserAccountRepository users,
    WardrobeService wardrobe,
    TryOnService tryOn,
    IStoredPhotoDeletion photoDeletion,
    ILogger<Program> logger,
    HttpContext context) =>
{
    var userId = CurrentUser(context);
    PurgeAccountData(userId, users, wardrobe, tryOn, photoDeletion, logger);
    var deleted = users.DeleteUserById(userId);
    ClearAuthCookies(context, app.Environment);
    return deleted ? Results.NoContent() : Results.NotFound();
});

// Admin panel endpoints. Session middleware has already authenticated the caller; every
// handler additionally requires the effective Admin role resolved for this request.
api.MapGet("/admin/stats", (AdminService admin, HttpContext context) =>
{
    if (RequireAdmin(context) is { } forbidden)
    {
        return forbidden;
    }

    var stats = admin.Stats();
    return Results.Ok(new AdminStatsResponse(stats.TotalUsers, stats.TotalGarments, stats.TotalOutfits, stats.TotalTryOnJobs));
})
    .Produces<AdminStatsResponse>(StatusCodes.Status200OK);

api.MapGet("/admin/users", (
    string? q,
    UserRole? role,
    int? offset,
    int? limit,
    AdminService admin,
    IStoredPhotoUrlRefresher photoUrls,
    HttpContext context) =>
{
    if (RequireAdmin(context) is { } forbidden)
    {
        return forbidden;
    }

    var page = admin.ListUsers(q, role, offset ?? 0, limit ?? AdminService.DefaultPageSize);
    return Results.Ok(new AdminUsersPageResponse(
        page.Items.Select(record => ToAdminUserResponse(record, admin, photoUrls, context.Request)).ToArray(),
        page.TotalCount,
        page.Offset,
        page.Limit));
})
    .Produces<AdminUsersPageResponse>(StatusCodes.Status200OK);

api.MapGet("/admin/users/{userId}", (string userId, AdminService admin, IStoredPhotoUrlRefresher photoUrls, HttpContext context) =>
{
    if (RequireAdmin(context) is { } forbidden)
    {
        return forbidden;
    }

    var record = admin.GetUser(userId);
    return record is null
        ? Results.NotFound()
        : Results.Ok(ToAdminUserResponse(record, admin, photoUrls, context.Request));
})
    .Produces<AdminUserResponse>(StatusCodes.Status200OK);

api.MapPut("/admin/users/{userId}/role", (
    string userId,
    UpdateUserRoleRequest request,
    AdminService admin,
    IStoredPhotoUrlRefresher photoUrls,
    HttpContext context) =>
{
    if (RequireAdmin(context) is { } forbidden)
    {
        return forbidden;
    }

    try
    {
        var record = admin.ChangeRole(CurrentUser(context), userId, request.Role);
        return record is null
            ? Results.NotFound()
            : Results.Ok(ToAdminUserResponse(record, admin, photoUrls, context.Request));
    }
    catch (ValidationException ex)
    {
        // Pinned targets and self-changes are business conflicts, not malformed input.
        return Results.Conflict(new { error = ex.Message });
    }
})
    .Produces<AdminUserResponse>(StatusCodes.Status200OK);

api.MapPost("/admin/users/{userId}/credits", (
    string userId,
    AdjustUserCreditsRequest request,
    IUserAccountRepository users,
    CreditLedgerService credits,
    HttpContext context) =>
{
    if (RequireAdmin(context) is { } forbidden)
    {
        return forbidden;
    }

    var target = users.GetUserById(userId);
    if (target is null)
    {
        return Results.NotFound();
    }

    try
    {
        var balance = credits.AdminAdjust(target, request.Delta);
        return Results.Ok(new { balance });
    }
    catch (ValidationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

api.MapPost("/admin/users/{userId}/sessions/revoke", (
    string userId,
    IUserAccountRepository users,
    IClock clock,
    HttpContext context) =>
{
    if (RequireAdmin(context) is { } forbidden)
    {
        return forbidden;
    }

    if (users.GetUserById(userId) is null)
    {
        return Results.NotFound();
    }

    users.RevokeAuthSessionsByUser(userId, clock.UtcNow);
    return Results.Ok(new { status = "sessions-revoked" });
});

api.MapPost("/admin/users/{userId}/purge-ai-outputs", (
    string userId,
    IUserAccountRepository users,
    TryOnService tryOn,
    HttpContext context) =>
{
    if (RequireAdmin(context) is { } forbidden)
    {
        return forbidden;
    }

    if (users.GetUserById(userId) is null)
    {
        return Results.NotFound();
    }

    return Results.Ok(new { purged = tryOn.PurgeAiOutputs(userId) });
});

api.MapGet("/admin/users/{userId}/export", (
    string userId,
    AdminService admin,
    IGarmentRepository garments,
    IOutfitRepository outfits,
    IBodyReferencePhotoRepository bodyPhotos,
    ITryOnJobRepository tryOnJobs,
    IStoredPhotoUrlRefresher photoUrls,
    HttpContext context) =>
{
    if (RequireAdmin(context) is { } forbidden)
    {
        return forbidden;
    }

    var record = admin.GetUser(userId);
    if (record is null)
    {
        return Results.NotFound();
    }

    // Same shape as the self-service /account/export, but the account record is the sanitized
    // admin DTO (no password hash / avatar object key).
    return Results.Ok(new
    {
        user = ToAdminUserResponse(record, admin, photoUrls, context.Request),
        garments = garments.ListGarmentsByUser(userId),
        outfits = outfits.ListOutfitsByUser(userId),
        bodyReferencePhotos = bodyPhotos.ListBodyReferencePhotosByUser(userId),
        tryOnJobs = tryOnJobs.ListTryOnJobsByUser(userId)
    });
});

api.MapDelete("/admin/users/{userId}", (
    string userId,
    AdminService admin,
    IUserAccountRepository users,
    WardrobeService wardrobe,
    TryOnService tryOn,
    IStoredPhotoDeletion photoDeletion,
    ILogger<Program> logger,
    HttpContext context) =>
{
    if (RequireAdmin(context) is { } forbidden)
    {
        return forbidden;
    }

    try
    {
        var target = admin.RequireDeletableUser(CurrentUser(context), userId);
        if (target is null)
        {
            return Results.NotFound();
        }

        PurgeAccountData(target.Id, users, wardrobe, tryOn, photoDeletion, logger);
        return users.DeleteUserById(target.Id) ? Results.NoContent() : Results.NotFound();
    }
    catch (ValidationException ex)
    {
        return Results.Conflict(new { error = ex.Message });
    }
});

api.MapPost("/body-reference-photos", (CreateBodyReferencePhotoRequest request, WardrobeService wardrobe, IStoredPhotoUrlRefresher photoUrls, HttpContext context) =>
{
    try
    {
        var photo = wardrobe.CreateBodyReferencePhoto(CurrentUser(context), request.ImageUrl);
        return Results.Created($"/api/body-reference-photos/{photo.Id}", ToBodyReferencePhotoResponse(photo, photoUrls, context.Request));
    }
    catch (ValidationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
    .Produces<BodyReferencePhoto>(StatusCodes.Status201Created);

api.MapGet("/body-reference-photos", (WardrobeService wardrobe, IStoredPhotoUrlRefresher photoUrls, HttpContext context) =>
    Results.Ok(wardrobe.ListBodyReferencePhotos(CurrentUser(context))
        .Select(photo => ToBodyReferencePhotoResponse(photo, photoUrls, context.Request))
        .ToArray()))
    .Produces<IReadOnlyList<BodyReferencePhoto>>(StatusCodes.Status200OK);

api.MapDelete("/body-reference-photos/{photoId:guid}", (Guid photoId, WardrobeService wardrobe, HttpContext context) =>
    wardrobe.DeleteBodyReferencePhoto(CurrentUser(context), photoId) ? Results.NoContent() : Results.NotFound());

api.MapGet("/garments", (
    WardrobeService wardrobe,
    IStoredPhotoUrlRefresher photoUrls,
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
        material))
        .Select(garment => ToGarmentResponse(garment, photoUrls, context.Request))
        .ToArray()))
    .Produces<IReadOnlyList<GarmentItem>>(StatusCodes.Status200OK);

api.MapGet("/garments/{garmentId:guid}", (Guid garmentId, WardrobeService wardrobe, IStoredPhotoUrlRefresher photoUrls, HttpContext context) =>
{
    var garment = wardrobe.GetGarment(CurrentUser(context), garmentId);
    return garment is null ? Results.NotFound() : Results.Ok(ToGarmentResponse(garment, photoUrls, context.Request));
})
    .Produces<GarmentItem>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status404NotFound);

api.MapPost("/uploads/garment-photo", async (HttpRequest request, PhotoUploadService photos, ILogger<Program> logger, CancellationToken cancellationToken) =>
{
    return await UploadPhoto(request, logger, "garment", cancellationToken, photo => photos.UploadGarmentPhoto(photo));
});

// Fast path for the non-blocking add flow: stores the ORIGINAL + thumbnail only (no rembg) and
// returns immediately; background removal then runs asynchronously after the garment is created.
api.MapPost("/uploads/garment-original", async (HttpRequest request, PhotoUploadService photos, ILogger<Program> logger, CancellationToken cancellationToken) =>
{
    return await UploadPhoto(request, logger, "garment", cancellationToken, photo => photos.UploadGarmentOriginal(photo));
});

// Auto-tag suggestions for a freshly uploaded garment photo. Client-orchestrated after the
// row's cutout/original is ready (concurrency-limited + abortable, like eager removal). Does
// NOT block the upload endpoints. Always returns 200: an unavailable/disabled tagger yields
// IsAvailable=false with empty suggestions, so prefill silently no-ops and manual entry works.
api.MapPost("/uploads/garment-photo/classify", (ClassifyGarmentPhotoRequest request, GarmentAutoTagService autoTagger, ILogger<Program> logger, HttpContext context) =>
{
    if (string.IsNullOrWhiteSpace(request.ImageUrl))
    {
        return Results.BadRequest(new { error = "imageUrl is required." });
    }

    var result = autoTagger.Classify(request.ImageUrl, request.KnownTags ?? Array.Empty<string>());
    if (!result.IsAvailable)
    {
        logger.LogDebug("Auto-tagging unavailable for trace {TraceId} (provider {Provider}).", context.TraceIdentifier, result.ProviderName);
    }

    return Results.Ok(new GarmentAutoTagResponse(
        result.IsAvailable,
        result.ProviderName,
        result.Category,
        result.CategoryConfidence,
        result.Colors.Select(color => new AutoTagColorResponse(color.Name, color.Hex, color.Confidence)).ToArray(),
        result.Seasons.Select(season => new AutoTagSuggestionResponse(season.Value, season.Confidence)).ToArray(),
        result.Tags.Select(tag => new AutoTagSuggestionResponse(tag.Value, tag.Confidence)).ToArray()));
})
    .Produces<GarmentAutoTagResponse>(StatusCodes.Status200OK);

api.MapPost("/uploads/body-reference-photo", async (HttpRequest request, PhotoUploadService photos, ILogger<Program> logger, CancellationToken cancellationToken) =>
{
    return await UploadPhoto(request, logger, "body-reference", cancellationToken, photo => photos.UploadBodyReferencePhoto(photo));
});

api.MapPost("/garments", (CreateGarmentRequest request, WardrobeService wardrobe, IStoredPhotoUrlRefresher photoUrls, HttpContext context) =>
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
            request.LaundryStatus,
            request.PerceptualHash,
            request.BackgroundRemovalPending ?? false,
            request.CutoutWidthPx,
            request.CutoutHeightPx));

        return Results.Created($"/api/garments/{garment.Id}", ToGarmentResponse(garment, photoUrls, context.Request));
    }
    catch (ValidationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
    .Produces<GarmentItem>(StatusCodes.Status201Created);

api.MapPatch("/garments/{garmentId:guid}", (Guid garmentId, UpdateGarmentRequest request, WardrobeService wardrobe, IStoredPhotoUrlRefresher photoUrls, HttpContext context) =>
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
            request.LaundryStatus,
            request.RotationDegrees));
        return garment is null ? Results.NotFound() : Results.Ok(ToGarmentResponse(garment, photoUrls, context.Request));
    }
    catch (ValidationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
    .Produces<GarmentItem>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status404NotFound);

api.MapDelete("/garments/{garmentId:guid}", (Guid garmentId, WardrobeService wardrobe, HttpContext context) =>
    wardrobe.DeleteGarment(CurrentUser(context), garmentId) ? Results.NoContent() : Results.NotFound());

api.MapGet("/hairstyles", (IHairstylePresetCatalog hairstyles, IUserAccountRepository users, HttpContext context) =>
{
    // Hairstyle presets are gender-specific; an account without a chosen gender gets an empty
    // catalog (the UI prompts for gender elsewhere, mirroring the AI try-on gating).
    var gender = users.GetUserById(CurrentUser(context))?.Gender;
    var presets = gender is null
        ? Array.Empty<HairstylePresetResponse>()
        : hairstyles.ListHairstylePresets(gender.Value)
            .Select(preset => ToHairstylePresetResponse(preset, context.Request))
            .ToArray();
    return Results.Ok(presets);
})
    .Produces<IReadOnlyList<HairstylePresetResponse>>(StatusCodes.Status200OK);

// Static, openly licensed preset images; anonymous GET (see RequiresAuthenticatedUser), same
// serving shape as /uploads/garments/{fileName}. Only manifest-listed file names resolve.
api.MapGet("/hairstyles/assets/{fileName}", (string fileName, IHairstylePresetCatalog hairstyles) =>
{
    var asset = hairstyles.GetHairstyleAssetFile(fileName);
    return asset is null ? Results.NotFound() : Results.File(asset.FullPath, asset.ContentType);
});

api.MapGet("/outfits", (
    OutfitService outfits,
    IStoredPhotoUrlRefresher photoUrls,
    IHairstylePresetCatalog hairstyles,
    HttpContext context,
    string? q,
    string? occasion,
    bool? favorite,
    bool? archived,
    string? sort,
    int? offset,
    int? limit) =>
    Results.Ok(outfits.ListOutfits(CurrentUser(context), new OutfitQuery(q, occasion, favorite, archived, sort, offset, limit))
        .Select(outfit => ToOutfitResponse(outfit, photoUrls, hairstyles, context.Request))
        .ToArray()))
    .Produces<IReadOnlyList<Outfit>>(StatusCodes.Status200OK);

api.MapGet("/outfits/{outfitId:guid}", (Guid outfitId, OutfitService outfits, IStoredPhotoUrlRefresher photoUrls, IHairstylePresetCatalog hairstyles, HttpContext context) =>
{
    var outfit = outfits.GetOutfit(CurrentUser(context), outfitId);
    return outfit is null ? Results.NotFound() : Results.Ok(ToOutfitResponse(outfit, photoUrls, hairstyles, context.Request));
})
    .Produces<Outfit>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status404NotFound);

api.MapPost("/outfits", (CreateOutfitRequest request, OutfitService outfits, IStoredPhotoUrlRefresher photoUrls, IHairstylePresetCatalog hairstyles, HttpContext context) =>
{
    try
    {
        var outfit = outfits.CreateOutfit(
            CurrentUser(context),
            request.Name,
            request.GarmentIds,
            request.HairstylePresetId,
            request.HairstyleVisible ?? true,
            request.SilhouetteGender);
        return Results.Created($"/api/outfits/{outfit.Id}", ToOutfitResponse(outfit, photoUrls, hairstyles, context.Request));
    }
    catch (ValidationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
    .Produces<Outfit>(StatusCodes.Status201Created);

api.MapPatch("/outfits/{outfitId:guid}", (Guid outfitId, UpdateOutfitRequest request, OutfitService outfits, IStoredPhotoUrlRefresher photoUrls, IHairstylePresetCatalog hairstyles, HttpContext context) =>
{
    try
    {
        var outfit = outfits.UpdateOutfit(CurrentUser(context), outfitId, new UpdateOutfitCommand(
            request.Name,
            request.GarmentIds,
            request.Tags,
            request.Occasion,
            request.IsFavorite,
            request.IsArchived,
            request.HairstylePresetId,
            request.HairstyleVisible,
            request.SilhouetteGender));
        return outfit is null ? Results.NotFound() : Results.Ok(ToOutfitResponse(outfit, photoUrls, hairstyles, context.Request));
    }
    catch (ValidationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
    .Produces<Outfit>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status404NotFound);

api.MapDelete("/outfits/{outfitId:guid}", (Guid outfitId, OutfitService outfits, HttpContext context) =>
    outfits.DeleteOutfit(CurrentUser(context), outfitId) ? Results.NoContent() : Results.NotFound());

api.MapDelete("/outfits/{outfitId:guid}/try-on-preview", (Guid outfitId, TryOnService tryOn, HttpContext context) =>
    tryOn.DeleteActiveOutfitOutput(CurrentUser(context), outfitId) ? Results.NoContent() : Results.NotFound());

api.MapPost("/outfits/{outfitId:guid}/try-on/estimate", (
    Guid outfitId,
    EstimateTryOnRequest request,
    TryOnService tryOn,
    IUserAccountRepository users,
    CreditLedgerService credits,
    HttpContext context) =>
{
    try
    {
        var userId = CurrentUser(context);
        var estimate = tryOn.Estimate(
            userId,
            outfitId,
            request.TryOnMode,
            request.BodyReferencePhotoUrl,
            request.BodyReferencePhotoId);
        var balance = users.GetUserById(userId) is { } user ? credits.GetBalance(user) : null;
        return Results.Ok(ToTryOnEstimateResponse(estimate, balance));
    }
    catch (ValidationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
    .Produces<TryOnEstimateResponse>(StatusCodes.Status200OK);

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
            request.TryOnMode,
            request.ConfirmedCredits,
            request.ConfirmedCacheKey,
            request.BodyReferencePhotoId,
            cancellationToken);
        return Results.Accepted($"/api/try-on-jobs/{job.Id}", job);
    }
    catch (ValidationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
    .RequireRateLimiting("try-on-rate-limit")
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
    catch (ValidationException ex)
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
    catch (ValidationException ex)
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
    catch (ValidationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
    .Produces<ShareLinkResponse>(StatusCodes.Status200OK);

api.MapGet("/share/{token}", (string token, ShareService share, IStoredPhotoUrlRefresher photoUrls, IHairstylePresetCatalog hairstyles, HttpContext context) =>
{
    var outfit = share.GetSharedOutfit(token);
    var responseOutfit = outfit is null ? null : ToOutfitResponse(outfit, photoUrls, hairstyles, context.Request);
    return outfit is null
        ? Results.NotFound()
        : Results.Ok(new SharedOutfitResponse(
            responseOutfit!.Id,
            responseOutfit.Name,
            responseOutfit.Items,
            responseOutfit.Tags,
            responseOutfit.Occasion,
            responseOutfit.IsFavorite,
            responseOutfit.IsArchived,
            responseOutfit.ClothesOnlyPreviewUrl,
            responseOutfit.PersonPreviewUrl,
            responseOutfit.CreatedAt,
            responseOutfit.HairstylePresetId,
            responseOutfit.HairstyleVisible,
            responseOutfit.SilhouetteGender,
            responseOutfit.HairstyleAssetUrl));
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
    try
    {
        var stored = await StoreMultipartPhoto(request, logger, uploadKind, cancellationToken, store);
        var publicUrl = PublicUploadUrl(request, stored.Url)
            ?? throw new InvalidOperationException("Stored upload URL is required.");
        return Results.Created(publicUrl, new UploadedPhotoResponse(
            stored.FileName,
            stored.ContentType,
            stored.Length,
            publicUrl,
            PublicUploadUrl(request, stored.OriginalUrl),
            PublicUploadUrl(request, stored.ThumbnailUrl),
            PublicUploadUrl(request, stored.ProcessedCutoutUrl),
            PublicUploadUrl(request, stored.SegmentationMaskUrl),
            stored.PerceptualHash,
            stored.CutoutMeasurement?.WidthPx,
            stored.CutoutMeasurement?.HeightPx));
    }
    catch (ValidationException ex)
    {
        logger.LogWarning(ex, "Upload diagnostics: rejected {Kind} upload trace {TraceId}", uploadKind, request.HttpContext.TraceIdentifier);
        return Results.BadRequest(new { error = ex.Message });
    }
}

static async Task<StoredPhoto> StoreMultipartPhoto(HttpRequest request, ILogger logger, string uploadKind, CancellationToken cancellationToken, Func<IncomingPhoto, StoredPhoto> store)
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
        throw new InvalidOperationException("Upload must use multipart form data.");
    }

    var form = await request.ReadFormAsync(cancellationToken);
    var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
    if (file is null)
    {
        throw new InvalidOperationException("Photo file is required.");
    }

    logger.LogInformation(
        "Upload diagnostics: processing {Kind} upload trace {TraceId}; fileName={FileName}; fileContentType={FileContentType}; fileLength={FileLength}",
        uploadKind,
        request.HttpContext.TraceIdentifier,
        file.FileName,
        file.ContentType,
        file.Length);

    await using var stream = file.OpenReadStream();
    var stored = store(new IncomingPhoto(file.FileName, file.ContentType, file.Length, stream));
    logger.LogInformation(
        "Upload diagnostics: stored {Kind} upload trace {TraceId}; storedFileName={StoredFileName}; publicUrl={PublicUrl}",
        uploadKind,
        request.HttpContext.TraceIdentifier,
        stored.FileName,
        PublicUploadUrl(request, stored.Url));
    return stored;
}

static string? PublicUploadUrl(HttpRequest request, string? storedUrl)
{
    if (string.IsNullOrWhiteSpace(storedUrl))
    {
        return null;
    }

    return Uri.TryCreate(storedUrl, UriKind.Absolute, out _)
        ? storedUrl
        : $"{request.Scheme}://{request.Host}{storedUrl}";
}

static BodyReferencePhoto ToBodyReferencePhotoResponse(BodyReferencePhoto photo, IStoredPhotoUrlRefresher photoUrls, HttpRequest request)
{
    return photo with
    {
        ImageUrl = PublicUploadUrl(request, photoUrls.RefreshBodyReferencePhotoUrl(photo.ImageUrl)) ?? photo.ImageUrl
    };
}

static HairstylePresetResponse ToHairstylePresetResponse(HairstylePreset preset, HttpRequest request)
{
    var assetPath = $"/api/hairstyles/assets/{Uri.EscapeDataString(preset.AssetFileName)}";
    return new HairstylePresetResponse(
        preset.Id,
        preset.Name,
        preset.Gender,
        preset.SortOrder,
        PublicUploadUrl(request, assetPath) ?? assetPath);
}

static GarmentItem ToGarmentResponse(GarmentItem garment, IStoredPhotoUrlRefresher photoUrls, HttpRequest request)
{
    var imageUrl = PublicUploadUrl(request, photoUrls.RefreshGarmentImageUrl(garment.ImageUrl)) ?? garment.ImageUrl;
    var thumbnailUrl = PublicUploadUrl(request, photoUrls.RefreshGarmentThumbnailUrl(garment.ThumbnailUrl)) ?? garment.ThumbnailUrl;
    return garment with
    {
        ImageUrl = imageUrl,
        ThumbnailUrl = thumbnailUrl
    };
}

static Outfit ToOutfitResponse(Outfit outfit, IStoredPhotoUrlRefresher photoUrls, IHairstylePresetCatalog hairstyles, HttpRequest request)
{
    return outfit with
    {
        Items = outfit.Items
            .Select(item => ToOutfitItemResponse(item, photoUrls, request))
            .ToArray(),
        HairstyleAssetUrl = ResolveHairstyleAssetUrl(outfit.HairstylePresetId, hairstyles, request)
    };
}

// Resolves the worn preset's public asset URL so cards and the anonymous shared view can render
// the hairstyle without calling the (authenticated) preset listing.
static string? ResolveHairstyleAssetUrl(string? hairstylePresetId, IHairstylePresetCatalog hairstyles, HttpRequest request)
{
    if (string.IsNullOrWhiteSpace(hairstylePresetId) || hairstyles.FindHairstylePreset(hairstylePresetId) is not { } preset)
    {
        return null;
    }

    var assetPath = $"/api/hairstyles/assets/{Uri.EscapeDataString(preset.AssetFileName)}";
    return PublicUploadUrl(request, assetPath) ?? assetPath;
}

static OutfitItem ToOutfitItemResponse(OutfitItem item, IStoredPhotoUrlRefresher photoUrls, HttpRequest request)
{
    return item with
    {
        ThumbnailUrl = PublicUploadUrl(request, photoUrls.RefreshGarmentThumbnailUrl(item.ThumbnailUrl)) ?? item.ThumbnailUrl
    };
}

static ITryOnProvider CreateTryOnProvider(IServiceProvider provider, IConfiguration configuration)
{
    var configuredProvider = configuration["TryOn:Provider"] ?? "Mock";
    var httpFactory = provider.GetRequiredService<IHttpClientFactory>();

    return configuredProvider.Trim().ToLowerInvariant() switch
    {
        "fashn" or "fashntryonprovider" => new FashnTryOnProvider(
            httpFactory.CreateClient("fashn"),
            new FashnTryOnSettings(
                configuration["Fashn:ApiKey"] ?? "",
                configuration["Fashn:ModelName"] ?? "tryon-max",
                configuration["Fashn:Mode"] ?? "quality",
                configuration.GetValue("Fashn:MaxPollingAttempts", 30),
                TimeSpan.FromSeconds(configuration.GetValue("Fashn:PollIntervalSeconds", 2)),
                configuration.GetValue("Fashn:NumSamples", 1),
                configuration["Fashn:OutputFormat"] ?? "png",
                configuration.GetValue("Fashn:ReturnBase64", false),
                configuration.GetValue("Fashn:SegmentationFree", true),
                configuration["Fashn:GarmentPhotoType"] ?? "auto",
                configuration.GetValue<int?>("Fashn:Seed"),
                configuration["Fashn:Resolution"] ?? "1k",
                configuration["Fashn:GenderPromptTemplate"] ?? "")),
        "localvton" or "local-vton" or "localvtonprovider" => new LocalVtonProvider(
            httpFactory.CreateClient("local-vton"),
            HttpProviderSettings(configuration, "LocalVton", "http://localhost:7860/", "try-on", requiresApiKey: false)),
        "localcatvton" or "local-cat-vton" or "localcatvtonprovider" => new LocalCatVtonProvider(
            httpFactory.CreateClient("local-cat-vton"),
            HttpProviderSettings(configuration, "LocalCatVton", "http://localhost:7861/", "try-on", requiresApiKey: false)),
        "replicate" or "replicateprovider" => new ReplicateProvider(
            httpFactory.CreateClient("replicate"),
            HttpProviderSettings(configuration, "Replicate", "https://api.replicate.com/v1/", "predictions", requiresApiKey: true)),
        "fal" or "falprovider" => new FalProvider(
            httpFactory.CreateClient("fal"),
            HttpProviderSettings(configuration, "Fal", "https://fal.run/", "try-on", requiresApiKey: true)),
        "compositefashn" or "composite-fashn" or "compositefashntryonprovider" => new CompositeFashnTryOnProvider(
            httpFactory.CreateClient("composite-fashn"),
            HttpProviderSettings(configuration, "CompositeFashn", "https://api.fashn.ai/v1/", "try-on", requiresApiKey: true)),
        "selfhostedcatvton" or "self-hosted-catvton" or "selfhostedcatvtonprovider" => new SelfHostedCatVtonProvider(
            httpFactory.CreateClient("self-hosted-catvton"),
            HttpProviderSettings(configuration, "SelfHostedCatVton", "http://localhost:7861/", "try-on", requiresApiKey: false)),
        "generalimageedit" or "general-image-edit" or "generalimageedittryonprovider" => new GeneralImageEditTryOnProvider(
            httpFactory.CreateClient("general-image-edit"),
            HttpProviderSettings(configuration, "GeneralImageEdit", "https://api.openai.com/v1/", "images/edits", requiresApiKey: true)),
        _ => new MockTryOnProvider()
    };
}

// Fail fast at startup when a selected provider is missing its required credentials, rather than
// only failing on the first request that needs them. Skipped during build-time OpenAPI generation.
static void EnsureProviderConfiguration(IConfiguration configuration)
{
    if (IsOpenApiDocumentGeneration())
    {
        return;
    }

    var tryOnProvider = (configuration["TryOn:Provider"] ?? "Mock").Trim().ToLowerInvariant();
    if (tryOnProvider is "fashn" or "fashntryonprovider" or "compositefashn" or "composite-fashn" or "compositefashntryonprovider"
        && string.IsNullOrWhiteSpace(configuration["Fashn:ApiKey"]))
    {
        throw new InvalidOperationException("Fashn:ApiKey must be configured when TryOn:Provider selects a FASHN provider.");
    }

    var objectStorageProvider = (configuration["ObjectStorage:Provider"] ?? "Local").Trim().ToLowerInvariant();
    if (objectStorageProvider is "s3" or "minio")
    {
        var endpoint = configuration["ObjectStorage:S3:Endpoint"] ?? configuration["Minio:Endpoint"];
        var accessKey = configuration["ObjectStorage:S3:AccessKey"] ?? configuration["Minio:AccessKey"];
        var secretKey = configuration["ObjectStorage:S3:SecretKey"] ?? configuration["Minio:SecretKey"];
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(accessKey) || string.IsNullOrWhiteSpace(secretKey))
        {
            throw new InvalidOperationException("ObjectStorage:S3 endpoint, access key, and secret key must be configured when ObjectStorage:Provider is S3 or Minio.");
        }
    }
}

// Fail fast at startup rather than letting LocalObjectStorage silently fall back to its
// source-visible development signing key. Without a real secret, anyone who can read the
// repository could forge signed URLs and reach private body-reference photos.
static void EnsureLocalObjectStorageSigningSecret(IConfiguration configuration, IWebHostEnvironment environment)
{
    // Build-time OpenAPI generation boots the host without deploy secrets; don't fail it.
    if (IsOpenApiDocumentGeneration())
    {
        return;
    }

    var provider = (configuration["ObjectStorage:Provider"] ?? "Local").Trim().ToLowerInvariant();
    if (provider is "s3" or "minio" || environment.IsDevelopment())
    {
        return;
    }

    if (string.IsNullOrWhiteSpace(configuration["ObjectStorage:Local:SigningSecret"]))
    {
        throw new InvalidOperationException(
            "ObjectStorage:Local:SigningSecret must be configured outside Development. " +
            "Without it, local object-storage signed URLs are signed with a source-visible development key and can be forged to reach private body-reference photos.");
    }
}

static IObjectStorage CreateObjectStorage(IConfiguration configuration, IWebHostEnvironment environment, string? publicOrigin)
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
    return new LocalObjectStorage(root, configuration["ObjectStorage:Local:SigningSecret"], publicOrigin);
}

static FileBackedOutfitStore CreateLocalOutfitStore(IConfiguration configuration, IWebHostEnvironment environment)
{
    var snapshotPath = configuration["Storage:Local:DataPath"]
        ?? Path.Combine(environment.ContentRootPath, "storage", "outfit-store.json");
    return new FileBackedOutfitStore(snapshotPath);
}

static IBackgroundRemovalProvider CreateBackgroundRemovalProvider(IServiceProvider provider, IConfiguration configuration)
{
    var configuredProvider = (configuration["BackgroundRemoval:Provider"] ?? "Auto").Trim().ToLowerInvariant();
    var httpSection = BackgroundRemovalHttpSection(configuredProvider);
    return configuredProvider switch
    {
        "auto" => new AutoBackgroundRemovalProvider(
            CreateRembgBackgroundRemovalProvider(configuration),
            new SimpleBackgroundRemovalProvider(),
            () => IsExecutableAvailable(BackgroundRemovalSetting(configuration, "Rembg", "ExecutablePath", "rembg"))),
        "rembg" or "rembgcommand" or "rembg-command" or "rembgexecutable" or "rembg-executable" => CreateRembgBackgroundRemovalProvider(configuration),
        "rembgserver" or "rembg-server" or "rembghttp" or "rembg-http" => CreateRembgServerBackgroundRemovalProvider(provider, configuration),
        "http" or "api" or "cloudflare" or "cloudflareimages" or "cloudflare-images" or "photoroom" or "removebg" or "remove-bg" or "clipdrop" => new HttpBackgroundRemovalProvider(
            provider.GetRequiredService<IHttpClientFactory>().CreateClient("background-removal"),
            new HttpBackgroundRemovalSettings(
                BackgroundRemovalSetting(configuration, httpSection, "Endpoint", ""),
                BackgroundRemovalSetting(configuration, httpSection, "ApiKey", ""),
                BackgroundRemovalSetting(configuration, httpSection, "ApiKeyHeader", DefaultBackgroundRemovalApiKeyHeader(configuredProvider)),
                BackgroundRemovalSetting(configuration, httpSection, "ApiKeyPrefix", DefaultBackgroundRemovalApiKeyPrefix(configuredProvider)),
                BackgroundRemovalSetting(configuration, httpSection, "ImageFieldName", "image_file"),
                TimeSpan.FromSeconds(BackgroundRemovalIntSetting(configuration, httpSection, "TimeoutSeconds", 120)))),
        _ => new SimpleBackgroundRemovalProvider()
    };
}

static RembgServerBackgroundRemovalProvider CreateRembgServerBackgroundRemovalProvider(IServiceProvider provider, IConfiguration configuration)
{
    return new RembgServerBackgroundRemovalProvider(
        provider.GetRequiredService<IHttpClientFactory>().CreateClient("background-removal"),
        new RembgServerBackgroundRemovalSettings(
            configuration["BackgroundRemoval:RembgServer:Endpoint"] ?? "http://127.0.0.1:7000/api/remove",
            BackgroundRemovalSetting(configuration, "RembgServer", "ImageFieldName", "file"),
            BackgroundRemovalSetting(
                configuration,
                "RembgServer",
                "ModelName",
                BackgroundRemovalSetting(configuration, "Rembg", "ModelName", "birefnet-general")),
            TimeSpan.FromSeconds(BackgroundRemovalIntSetting(configuration, "RembgServer", "TimeoutSeconds", 120))));
}

static RembgBackgroundRemovalProvider CreateRembgBackgroundRemovalProvider(IConfiguration configuration)
{
    return new RembgBackgroundRemovalProvider(
        new RembgBackgroundRemovalSettings(
            BackgroundRemovalSetting(configuration, "Rembg", "ExecutablePath", "rembg"),
            BackgroundRemovalSetting(configuration, "Rembg", "ModelName", "birefnet-general"),
            TimeSpan.FromSeconds(BackgroundRemovalIntSetting(configuration, "Rembg", "TimeoutSeconds", 180)),
            BackgroundRemovalOptionalSetting(configuration, "Rembg", "ModelHome")));
}

// Garment auto-tagging provider selection, mirroring background removal. Default "Auto" uses
// the local Python service when its /health endpoint is reachable and otherwise degrades to a
// no-op Disabled tagger, so uploads never depend on the service being up.
static IGarmentAutoTagger CreateGarmentAutoTagger(IServiceProvider provider, IConfiguration configuration)
{
    var configured = (configuration["AutoTagging:Provider"] ?? "Auto").Trim().ToLowerInvariant();
    return configured switch
    {
        "disabled" or "off" or "none" => new DisabledGarmentAutoTagger(),
        "httpserver" or "http-server" or "http" or "server" => CreateHttpGarmentAutoTagger(provider, configuration),
        "auto" => new AutoGarmentAutoTagger(
            CreateHttpGarmentAutoTagger(provider, configuration),
            new DisabledGarmentAutoTagger(),
            CreateAutoTagHealthProbe(provider, configuration).IsHealthy),
        _ => new DisabledGarmentAutoTagger()
    };
}

static HttpGarmentAutoTagger CreateHttpGarmentAutoTagger(IServiceProvider provider, IConfiguration configuration)
{
    return new HttpGarmentAutoTagger(
        provider.GetRequiredService<IHttpClientFactory>().CreateClient("auto-tagging"),
        new HttpGarmentAutoTaggerSettings(
            AutoTaggingClassifyEndpoint(configuration),
            TimeSpan.FromSeconds(AutoTaggingIntSetting(configuration, "TimeoutSeconds", 60))));
}

static GarmentAutoTagHealthProbe CreateAutoTagHealthProbe(IServiceProvider provider, IConfiguration configuration)
{
    var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("auto-tagging");
    client.Timeout = TimeSpan.FromSeconds(AutoTaggingIntSetting(configuration, "HealthTimeoutSeconds", 3));
    return new GarmentAutoTagHealthProbe(
        client,
        AutoTaggingHealthEndpoint(configuration),
        TimeSpan.FromSeconds(AutoTaggingIntSetting(configuration, "HealthCacheSeconds", 15)));
}

static string AutoTaggingClassifyEndpoint(IConfiguration configuration)
{
    return (configuration["AutoTagging:HttpServer:Endpoint"] ?? "http://127.0.0.1:7100/classify").Trim();
}

static string AutoTaggingHealthEndpoint(IConfiguration configuration)
{
    var configured = configuration["AutoTagging:HttpServer:HealthEndpoint"];
    return string.IsNullOrWhiteSpace(configured)
        ? DeriveAutoTagHealthEndpoint(AutoTaggingClassifyEndpoint(configuration))
        : configured.Trim();
}

static string DeriveAutoTagHealthEndpoint(string classifyEndpoint)
{
    if (string.IsNullOrWhiteSpace(classifyEndpoint))
    {
        return string.Empty;
    }

    const string classifySuffix = "/classify";
    if (classifyEndpoint.EndsWith(classifySuffix, StringComparison.OrdinalIgnoreCase))
    {
        return string.Concat(classifyEndpoint.AsSpan(0, classifyEndpoint.Length - classifySuffix.Length), "/health");
    }

    return Uri.TryCreate(classifyEndpoint, UriKind.Absolute, out var uri) ? new Uri(uri, "/health").ToString() : string.Empty;
}

static int AutoTaggingIntSetting(IConfiguration configuration, string key, int fallback)
{
    var value = configuration[$"AutoTagging:HttpServer:{key}"] ?? configuration[$"AutoTagging:{key}"];
    return int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
}

static bool IsExecutableAvailable(string executablePath)
{
    if (string.IsNullOrWhiteSpace(executablePath))
    {
        return false;
    }

    var trimmed = executablePath.Trim();
    if (Path.IsPathFullyQualified(trimmed)
        || trimmed.Contains(Path.DirectorySeparatorChar)
        || trimmed.Contains(Path.AltDirectorySeparatorChar))
    {
        return File.Exists(trimmed);
    }

    var path = Environment.GetEnvironmentVariable("PATH");
    if (string.IsNullOrWhiteSpace(path))
    {
        return false;
    }

    foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
    {
        foreach (var candidateName in CandidateExecutableNames(trimmed))
        {
            if (File.Exists(Path.Combine(directory, candidateName)))
            {
                return true;
            }
        }
    }

    return false;
}

static IEnumerable<string> CandidateExecutableNames(string executableName)
{
    yield return executableName;

    if (!OperatingSystem.IsWindows() || !string.IsNullOrWhiteSpace(Path.GetExtension(executableName)))
    {
        yield break;
    }

    var pathExtensions = Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD";
    foreach (var extension in pathExtensions.Split(';', StringSplitOptions.RemoveEmptyEntries))
    {
        yield return executableName + extension;
    }
}

static string BackgroundRemovalHttpSection(string provider)
{
    return provider switch
    {
        "cloudflare" or "cloudflareimages" or "cloudflare-images" => "CloudflareImages",
        "photoroom" => "PhotoRoom",
        "removebg" or "remove-bg" => "RemoveBg",
        "clipdrop" => "Clipdrop",
        _ => "Http"
    };
}

static string DefaultBackgroundRemovalApiKeyHeader(string provider)
{
    return provider is "cloudflare" or "cloudflareimages" or "cloudflare-images"
        ? "Authorization"
        : "X-Api-Key";
}

static string DefaultBackgroundRemovalApiKeyPrefix(string provider)
{
    return provider is "cloudflare" or "cloudflareimages" or "cloudflare-images"
        ? "Bearer "
        : "";
}

static string BackgroundRemovalSetting(IConfiguration configuration, string sectionName, string key, string fallback)
{
    return configuration[$"BackgroundRemoval:{sectionName}:{key}"]
        ?? configuration[$"BackgroundRemoval:Http:{key}"]
        ?? configuration[$"BackgroundRemoval:{key}"]
        ?? fallback;
}

static string? BackgroundRemovalOptionalSetting(IConfiguration configuration, string sectionName, string key)
{
    return configuration[$"BackgroundRemoval:{sectionName}:{key}"]
        ?? configuration[$"BackgroundRemoval:{key}"];
}

static int BackgroundRemovalIntSetting(IConfiguration configuration, string sectionName, string key, int fallback)
{
    var value = BackgroundRemovalSetting(configuration, sectionName, key, "");
    return int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
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

static UserRole CurrentUserRole(HttpContext context)
{
    return context.Items.TryGetValue(CurrentUserRoleItemKey, out var role) && role is UserRole value
        ? value
        : throw new InvalidOperationException("Authenticated user role was not resolved for this request.");
}

// Null means the caller may proceed; otherwise the 403 result to return as-is.
static IResult? RequireAdmin(HttpContext context)
{
    return CurrentUserRole(context) == UserRole.Admin
        ? null
        : Results.Json(new { error = "Admin role is required." }, statusCode: StatusCodes.Status403Forbidden);
}

static RolePinningOptions LoadRolePinningOptions(IConfiguration configuration)
{
    return new RolePinningOptions(
        SplitPinnedEmails(configuration["Roles:PinnedAdminEmails"], "dmytro.bolibok@gmail.com"),
        SplitPinnedEmails(configuration["Roles:PinnedPremiumEmails"], "olya.shaydur@gmail.com"));

    // Unset/blank config keeps the built-in pins, so the "always admin/premium" accounts
    // cannot be silently unpinned by an empty environment variable.
    static IReadOnlyList<string> SplitPinnedEmails(string? configured, string fallback)
    {
        var source = string.IsNullOrWhiteSpace(configured) ? fallback : configured;
        return source.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}

// Best-effort: erase the user's stored binaries before the cascade removes the rows that hold
// their object keys. A storage failure must not block account deletion (right to erasure).
// Shared by the self-service DELETE /account and the admin panel delete.
static void PurgeAccountData(
    string userId,
    IUserAccountRepository users,
    WardrobeService wardrobe,
    TryOnService tryOn,
    IStoredPhotoDeletion photoDeletion,
    ILogger logger)
{
    try
    {
        tryOn.PurgeAiOutputs(userId);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Account deletion: failed to purge AI outputs for the deleted account.");
    }

    try
    {
        wardrobe.PurgeUserStoredPhotos(userId);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Account deletion: failed to purge stored garment/body photos for the deleted account.");
    }

    try
    {
        if (users.GetUserById(userId)?.AvatarObjectKey is { } avatarObjectKey)
        {
            photoDeletion.DeleteAvatarPhoto(avatarObjectKey);
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Account deletion: failed to purge the stored avatar for the deleted account.");
    }
}

static AdminUserResponse ToAdminUserResponse(AdminUserRecord record, AdminService admin, IStoredPhotoUrlRefresher? photoUrls = null, HttpRequest? request = null)
{
    var user = record.User;
    var avatarUrl = user.AvatarUrl;
    if (!string.IsNullOrWhiteSpace(avatarUrl) && photoUrls is not null)
    {
        avatarUrl = photoUrls.RefreshAvatarUrl(avatarUrl);
        if (request is not null)
        {
            avatarUrl = PublicUploadUrl(request, avatarUrl) ?? avatarUrl;
        }
    }

    return new AdminUserResponse(
        user.Id,
        user.Email,
        user.DisplayName,
        user.Gender,
        admin.EffectiveRole(user),
        admin.IsPinned(user),
        user.CreatedAt,
        user.LastLoginAt,
        user.EmailVerifiedAt,
        record.GarmentCount,
        record.OutfitCount,
        record.TryOnJobCount,
        record.BodyReferencePhotoCount,
        record.ActiveSessionCount,
        avatarUrl,
        admin.RawCreditBalance(user),
        record.SubscriptionStatus,
        record.SubscriptionPeriodEnd);
}

static BillingStatusResponse ToBillingStatusResponse(BillingStatus status)
{
    return new BillingStatusResponse(
        status.Enabled,
        status.Provider,
        status.SubscriptionPriceConfigured,
        status.PremiumDisplayPrice,
        status.Subscription is { } subscription
            ? new BillingSubscriptionResponse(subscription.Status, subscription.CurrentPeriodEnd, subscription.PremiumActive)
            : null,
        status.TopUpPacks.Select(pack => new BillingTopUpPackResponse(pack.Id, pack.Credits, pack.DisplayPrice)).ToArray(),
        status.PortalAvailable);
}

static BillingOptions LoadBillingOptions(IConfiguration configuration)
{
    var origin = (configuration["Authentication:PublicOrigin"] ?? "").TrimEnd('/');
    var packs = configuration.GetSection("Stripe:TopUpPacks").GetChildren()
        .Select(section => new BillingTopUpPack(
            (section["Id"] ?? "").Trim(),
            int.TryParse(section["Credits"], out var credits) ? credits : 0,
            (section["PriceId"] ?? "").Trim(),
            NullIfWhiteSpace(section["DisplayPrice"])))
        .Where(pack => pack.Id.Length > 0 && pack.Credits > 0)
        .ToArray();
    return new BillingOptions(
        (configuration["Stripe:PremiumMonthlyPriceId"] ?? "").Trim(),
        NullIfWhiteSpace(configuration["Stripe:PremiumMonthlyDisplayPrice"]),
        packs,
        NullIfWhiteSpace(configuration["Stripe:SuccessUrl"]) ?? $"{origin}/upgrade?checkout=success",
        NullIfWhiteSpace(configuration["Stripe:CancelUrl"]) ?? $"{origin}/upgrade?checkout=cancelled",
        NullIfWhiteSpace(configuration["Stripe:PortalReturnUrl"]) ?? $"{origin}/upgrade");

    static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

static IBillingProvider CreateBillingProvider(IConfiguration configuration)
{
    var configuredProvider = (configuration["Billing:Provider"] ?? "Auto").Trim().ToLowerInvariant();
    var secretKey = (configuration["Stripe:SecretKey"] ?? "").Trim();
    var webhookSecret = (configuration["Stripe:WebhookSecret"] ?? "").Trim();
    return configuredProvider switch
    {
        "stripe" => CreateStripeProvider(),
        "disabled" or "none" or "off" => new OutfitPlanner.Infrastructure.Billing.DisabledBillingProvider(),
        // Auto: Stripe when credentials exist, softly disabled otherwise.
        _ => string.IsNullOrWhiteSpace(secretKey)
            ? new OutfitPlanner.Infrastructure.Billing.DisabledBillingProvider()
            : CreateStripeProvider()
    };

    IBillingProvider CreateStripeProvider()
    {
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            throw new InvalidOperationException("Stripe:SecretKey must be configured when Billing:Provider selects Stripe.");
        }

        return new OutfitPlanner.Infrastructure.Billing.StripeBillingProvider(
            new OutfitPlanner.Infrastructure.Billing.StripeBillingSettings(secretKey, webhookSecret));
    }
}

static PlanCatalog LoadPlanCatalog(IConfiguration configuration)
{
    var free = PlanCatalog.Default.For(UserRole.Free);
    var premium = PlanCatalog.Default.For(UserRole.Premium);
    return new PlanCatalog(
        free with
        {
            MaxGarments = ReadPlanCap(configuration["Paywall:Free:MaxGarments"], free.MaxGarments),
            MaxOutfits = ReadPlanCap(configuration["Paywall:Free:MaxOutfits"], free.MaxOutfits),
            MaxBodyReferencePhotos = ReadPlanCap(configuration["Paywall:Free:MaxBodyReferencePhotos"], free.MaxBodyReferencePhotos),
            TrialCredits = ReadPlanCount(configuration["Paywall:Free:TrialCredits"], free.TrialCredits)
        },
        premium with
        {
            MaxBodyReferencePhotos = ReadPlanCap(configuration["Paywall:Premium:MaxBodyReferencePhotos"], premium.MaxBodyReferencePhotos),
            MonthlyCredits = ReadPlanCount(configuration["Paywall:Premium:MonthlyCredits"], premium.MonthlyCredits)
        },
        PlanCatalog.Default.For(UserRole.Admin));

    // Caps: unset keeps the default, zero/negative means unlimited.
    static int? ReadPlanCap(string? value, int? fallback)
    {
        return int.TryParse(value, out var parsed) ? (parsed <= 0 ? null : parsed) : fallback;
    }

    static int ReadPlanCount(string? value, int fallback)
    {
        return int.TryParse(value, out var parsed) && parsed >= 0 ? parsed : fallback;
    }
}

static TryOnEstimateResponse ToTryOnEstimateResponse(TryOnCostEstimate estimate, CreditBalanceInfo? creditBalance = null)
{
    return new TryOnEstimateResponse(
        estimate.Mode,
        estimate.ProviderName,
        estimate.BodyTryOnItems.Select(ToEstimateItem).ToArray(),
        estimate.VisualOnlyItems.Select(ToEstimateItem).ToArray(),
        estimate.IncludedGarmentIds,
        estimate.ExcludedGarmentIds,
        estimate.EstimatedCredits,
        estimate.IsAvailable,
        estimate.RequiresAi,
        estimate.RequiresPremiumConfirmation,
        estimate.CacheKey,
        estimate.HasCachedResult,
        estimate.Summary,
        estimate.Warnings,
        estimate.RequiresUpgrade,
        creditBalance?.Unlimited ?? false,
        creditBalance is { Unlimited: false } info ? info.Balance : null);
}

static TryOnEstimateItemResponse ToEstimateItem(OutfitItem item)
{
    return new TryOnEstimateItemResponse(
        item.GarmentId,
        item.Name,
        item.Category,
        item.BodyZone,
        item.ThumbnailUrl);
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
        // The billing webhook authenticates via its provider signature, not a session.
        && !path.Equals("/billing/webhook", StringComparison.OrdinalIgnoreCase)
        && !path.StartsWith("/auth/", StringComparison.OrdinalIgnoreCase)
        && !path.StartsWith("/openapi/", StringComparison.OrdinalIgnoreCase)
        && !path.StartsWith("/storage/signed/", StringComparison.OrdinalIgnoreCase)
        && !(HttpMethods.IsGet(context.Request.Method) && path.StartsWith("/share/", StringComparison.OrdinalIgnoreCase))
        // Hairstyle preset assets are app-owned, openly licensed images (not user data).
        && !(HttpMethods.IsGet(context.Request.Method) && path.StartsWith("/hairstyles/assets/", StringComparison.OrdinalIgnoreCase));
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

static AuthSessionResponse ToAuthSessionResponse(AuthResult result, IStoredPhotoUrlRefresher? photoUrls = null, HttpRequest? request = null)
{
    return new AuthSessionResponse(ToAuthUserResponse(result.User, photoUrls, request), result.ExpiresAt);
}

static AuthSessionResponse ToAuthSessionResponseFromSession(AuthenticatedSession session, IStoredPhotoUrlRefresher? photoUrls = null, HttpRequest? request = null)
{
    return new AuthSessionResponse(ToAuthUserResponse(session.User, photoUrls, request), session.ExpiresAt);
}

static AuthUserResponse ToAuthUserResponse(PublicUser user, IStoredPhotoUrlRefresher? photoUrls = null, HttpRequest? request = null)
{
    var avatarUrl = user.AvatarUrl;
    if (!string.IsNullOrWhiteSpace(avatarUrl) && photoUrls is not null)
    {
        avatarUrl = photoUrls.RefreshAvatarUrl(avatarUrl);
        if (request is not null)
        {
            avatarUrl = PublicUploadUrl(request, avatarUrl) ?? avatarUrl;
        }
    }

    return new AuthUserResponse(user.Id, user.Email, user.DisplayName, user.Username, avatarUrl, user.Gender, user.Role);
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

static void LoadDotEnvConfigurationAliases(ConfigurationManager configuration, string contentRootPath, string[] args)
{
    var dotEnvPath = FindDotEnvPath(contentRootPath)
        ?? FindDotEnvPath(Directory.GetCurrentDirectory());
    var dotEnvValues = dotEnvPath is null
        ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        : ReadDotEnvValues(dotEnvPath);
    var aliases = new[]
    {
        ("FASHN_API_KEY", "Fashn:ApiKey"),
        ("FASHN_BASE_URL", "Fashn:BaseUrl"),
        ("FASHN_MODEL_NAME", "Fashn:ModelName"),
        ("FASHN_MODE", "Fashn:Mode"),
        ("FASHN_MAX_POLLING_ATTEMPTS", "Fashn:MaxPollingAttempts"),
        ("FASHN_POLL_INTERVAL_SECONDS", "Fashn:PollIntervalSeconds"),
        ("FASHN_TIMEOUT_SECONDS", "Fashn:TimeoutSeconds"),
        ("FASHN_NUM_SAMPLES", "Fashn:NumSamples"),
        ("FASHN_OUTPUT_FORMAT", "Fashn:OutputFormat"),
        ("FASHN_RETURN_BASE64", "Fashn:ReturnBase64"),
        ("FASHN_SEGMENTATION_FREE", "Fashn:SegmentationFree"),
        ("FASHN_GARMENT_PHOTO_TYPE", "Fashn:GarmentPhotoType"),
        ("FASHN_SEED", "Fashn:Seed"),
        ("FASHN_RESOLUTION", "Fashn:Resolution"),
        ("FASHN_GENDER_PROMPT_TEMPLATE", "Fashn:GenderPromptTemplate")
    };

    var mappedValues = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    foreach (var (sourceKey, configurationKey) in aliases)
    {
        var value = Environment.GetEnvironmentVariable(sourceKey);
        if (string.IsNullOrWhiteSpace(value) && dotEnvValues.TryGetValue(sourceKey, out var dotEnvValue))
        {
            value = dotEnvValue;
        }

        if (!string.IsNullOrWhiteSpace(value))
        {
            mappedValues[configurationKey] = value;
        }
    }

    if (mappedValues.Count == 0)
    {
        return;
    }

    configuration.AddInMemoryCollection(mappedValues);
    configuration.AddEnvironmentVariables();
    configuration.AddCommandLine(args);
}

static string? FindDotEnvPath(string startPath)
{
    if (string.IsNullOrWhiteSpace(startPath))
    {
        return null;
    }

    var directory = Directory.Exists(startPath)
        ? new DirectoryInfo(startPath)
        : new FileInfo(startPath).Directory;
    while (directory is not null)
    {
        var candidate = Path.Combine(directory.FullName, ".env");
        if (File.Exists(candidate))
        {
            return candidate;
        }

        directory = directory.Parent;
    }

    return null;
}

static Dictionary<string, string> ReadDotEnvValues(string path)
{
    var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var rawLine in File.ReadLines(path))
    {
        var line = rawLine.Trim();
        if (line.Length == 0 || line.StartsWith('#'))
        {
            continue;
        }

        if (line.StartsWith("export ", StringComparison.Ordinal))
        {
            line = line["export ".Length..].TrimStart();
        }

        var separatorIndex = line.IndexOf('=');
        if (separatorIndex <= 0)
        {
            continue;
        }

        var key = line[..separatorIndex].Trim();
        var value = line[(separatorIndex + 1)..].Trim();
        if (value.Length >= 2
            && ((value[0] == '"' && value[^1] == '"')
                || (value[0] == '\'' && value[^1] == '\'')))
        {
            value = value[1..^1];
        }

        if (key.Length > 0)
        {
            values[key] = value;
        }
    }

    return values;
}

static string BuildExternalCallbackUri(string publicOrigin, PathString callbackPath)
{
    return $"{publicOrigin}{callbackPath}";
}

public partial class Program;
