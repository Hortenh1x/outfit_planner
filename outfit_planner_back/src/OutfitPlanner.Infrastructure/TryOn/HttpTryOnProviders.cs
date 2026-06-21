using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using OutfitPlanner.Application.Abstractions;
using OutfitPlanner.Domain;

namespace OutfitPlanner.Infrastructure.TryOn;

public sealed record HttpTryOnProviderSettings(
    string BaseUrl,
    string Endpoint,
    string ApiKey,
    string ModelName,
    bool RequiresApiKey);

public sealed class LocalVtonProvider : JsonTryOnProvider
{
    public LocalVtonProvider(HttpClient http, HttpTryOnProviderSettings settings)
        : base(http, settings, "LocalVton")
    {
    }
}

public sealed class LocalCatVtonProvider : JsonTryOnProvider
{
    public LocalCatVtonProvider(HttpClient http, HttpTryOnProviderSettings settings)
        : base(http, settings, "LocalCatVton")
    {
    }
}

public sealed class ReplicateProvider : JsonTryOnProvider
{
    public ReplicateProvider(HttpClient http, HttpTryOnProviderSettings settings)
        : base(http, settings, "Replicate")
    {
    }
}

public sealed class FalProvider : JsonTryOnProvider
{
    public FalProvider(HttpClient http, HttpTryOnProviderSettings settings)
        : base(http, settings, "Fal")
    {
    }
}

public sealed class SelfHostedCatVtonProvider : JsonTryOnProvider
{
    public SelfHostedCatVtonProvider(HttpClient http, HttpTryOnProviderSettings settings)
        : base(http, settings, nameof(SelfHostedCatVtonProvider))
    {
    }
}

public sealed class CompositeFashnTryOnProvider : JsonTryOnProvider
{
    public CompositeFashnTryOnProvider(HttpClient http, HttpTryOnProviderSettings settings)
        : base(http, settings, nameof(CompositeFashnTryOnProvider))
    {
    }
}

public sealed class GeneralImageEditTryOnProvider : JsonTryOnProvider
{
    public GeneralImageEditTryOnProvider(HttpClient http, HttpTryOnProviderSettings settings)
        : base(http, settings, nameof(GeneralImageEditTryOnProvider))
    {
    }
}

public abstract class JsonTryOnProvider : ITryOnProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly HttpTryOnProviderSettings _settings;
    private readonly string _providerName;

    protected JsonTryOnProvider(HttpClient http, HttpTryOnProviderSettings settings, string providerName)
    {
        _http = http;
        _settings = settings;
        _providerName = providerName;

        if (!string.IsNullOrWhiteSpace(settings.BaseUrl))
        {
            _http.BaseAddress = new Uri(settings.BaseUrl);
        }
    }

    public string Name => _providerName;

    public TryOnProviderCapabilities Capabilities => new(
        Name,
        _settings.ModelName,
        "default",
        $"{_settings.ModelName}:default",
        new HashSet<TryOnMode>
        {
            TryOnMode.ClothesOnlyPreview,
            TryOnMode.SingleGarmentTryOn,
            TryOnMode.SequentialOutfitTryOn,
            TryOnMode.ExperimentalCompositeTryOn
        });

    public TryOnGeneration Generate(TryOnProviderRequest request)
    {
        if (_settings.RequiresApiKey && string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            throw new InvalidOperationException($"{_providerName} provider requires an API key.");
        }

        if (string.IsNullOrWhiteSpace(_settings.Endpoint))
        {
            throw new InvalidOperationException($"{_providerName} provider endpoint is not configured.");
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _settings.Endpoint);
        if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        }

        httpRequest.Content = JsonContent.Create(new HttpTryOnRequest(
            _settings.ModelName,
            request.UserId,
            request.OutfitId,
            request.BodyReferencePhotoUrl,
            request.Mode.ToString(),
            request.BodyTryOnItems.Select(item => new HttpTryOnGarment(
                item.GarmentId,
                item.Name,
                item.Category.ToString(),
                item.ThumbnailUrl)).ToArray(),
            request.VisualOnlyItems.Select(item => new HttpTryOnGarment(
                item.GarmentId,
                item.Name,
                item.Category.ToString(),
                item.ThumbnailUrl)).ToArray()), options: JsonOptions);

        using var response = _http.Send(httpRequest);
        var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"{_providerName} provider returned {(int)response.StatusCode}: {body}");
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var providerJobId = ReadString(root, "providerJobId")
            ?? ReadString(root, "provider_job_id")
            ?? ReadString(root, "id")
            ?? ReadString(root, "job_id")
            ?? $"{_providerName.ToLowerInvariant()}-{Guid.NewGuid():N}";
        var outputImageUrl = ReadString(root, "outputImageUrl")
            ?? ReadString(root, "output_image_url")
            ?? ReadString(root, "imageUrl")
            ?? ReadString(root, "image_url")
            ?? ReadFirstString(root, "output");

        if (string.IsNullOrWhiteSpace(outputImageUrl))
        {
            throw new InvalidOperationException($"{_providerName} provider response did not include an output image URL.");
        }

        return new TryOnGeneration(providerJobId, outputImageUrl);
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static string? ReadFirstString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Array when property.GetArrayLength() > 0 && property[0].ValueKind == JsonValueKind.String => property[0].GetString(),
            _ => null
        };
    }
}

file sealed record HttpTryOnRequest(
    [property: JsonPropertyName("model_name")] string ModelName,
    [property: JsonPropertyName("user_id")] string UserId,
    [property: JsonPropertyName("outfit_id")] Guid OutfitId,
    [property: JsonPropertyName("model_image")] string ModelImage,
    [property: JsonPropertyName("try_on_mode")] string TryOnMode,
    [property: JsonPropertyName("body_try_on_items")] IReadOnlyList<HttpTryOnGarment> BodyTryOnItems,
    [property: JsonPropertyName("visual_only_items")] IReadOnlyList<HttpTryOnGarment> VisualOnlyItems);

file sealed record HttpTryOnGarment(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("image_url")] string ImageUrl);
