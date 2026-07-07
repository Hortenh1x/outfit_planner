using System.Net.Http.Headers;
using System.Text.Json;
using OutfitPlanner.Application.Abstractions;
using OutfitPlanner.Domain;
using OutfitPlanner.Infrastructure.Storage;

namespace OutfitPlanner.Infrastructure.AutoTagging;

// Provider adapters for garment auto-tagging, mirroring the background-removal provider
// pattern: an HTTP adapter to the local Python service, a no-op Disabled provider, and an
// Auto wrapper that routes to the service when it is healthy and otherwise degrades to
// Disabled. Everything runs locally; images never leave the machine.

public sealed record HttpGarmentAutoTaggerSettings(string Endpoint, TimeSpan Timeout);

public sealed class HttpGarmentAutoTagger : IGarmentAutoTagger
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _client;
    private readonly HttpGarmentAutoTaggerSettings _settings;

    public HttpGarmentAutoTagger(HttpClient client, HttpGarmentAutoTaggerSettings settings)
    {
        _client = client;
        _settings = settings;
        if (settings.Timeout > TimeSpan.Zero)
        {
            _client.Timeout = settings.Timeout;
        }
    }

    public string Name => "http-autotag";

    public GarmentAutoTagResult Classify(GarmentAutoTagRequest request)
    {
        if (string.IsNullOrWhiteSpace(_settings.Endpoint))
        {
            throw new InvalidOperationException("AutoTagging:HttpServer:Endpoint must be configured.");
        }

        using var content = new MultipartFormDataContent();
        using var imageContent = new ByteArrayContent(request.ImageBytes);
        imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse(request.ContentType);
        content.Add(imageContent, "file", SafeFileName(request.FileName));
        foreach (var tag in request.KnownTags)
        {
            if (!string.IsNullOrWhiteSpace(tag))
            {
                content.Add(new StringContent(tag), "known_tags");
            }
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, _settings.Endpoint) { Content = content };
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = _client.Send(message);
        var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Auto-tagging provider returned {(int)response.StatusCode}: {TrimForError(body)}");
        }

        return Parse(body);
    }

    private GarmentAutoTagResult Parse(string json)
    {
        var dto = JsonSerializer.Deserialize<ClassifyResponseDto>(json, JsonOptions)
            ?? throw new InvalidOperationException("Auto-tagging provider returned an unreadable response.");

        var providerName = string.IsNullOrWhiteSpace(dto.Provider) ? Name : dto.Provider!;

        GarmentCategory? category = null;
        var categoryConfidence = 0d;
        if (dto.Category is { } categoryDto
            && !string.IsNullOrWhiteSpace(categoryDto.Value)
            && Enum.TryParse<GarmentCategory>(categoryDto.Value, ignoreCase: true, out var parsed)
            && Enum.IsDefined(parsed))
        {
            category = parsed;
            categoryConfidence = categoryDto.Confidence;
        }

        var colors = (dto.Colors ?? new List<ClassifyColorDto>())
            .Where(color => !string.IsNullOrWhiteSpace(color.Name))
            .Select(color => new AutoTagColorSuggestion(color.Name!.Trim(), (color.Hex ?? string.Empty).Trim(), color.Confidence))
            .ToArray();

        var seasons = ToSuggestions(dto.Seasons);
        var tags = ToSuggestions(dto.Tags);

        return new GarmentAutoTagResult(category, categoryConfidence, colors, seasons, tags, providerName);
    }

    private static AutoTagSuggestion[] ToSuggestions(List<ClassifyValueDto>? values)
    {
        return (values ?? new List<ClassifyValueDto>())
            .Where(value => !string.IsNullOrWhiteSpace(value.Value))
            .Select(value => new AutoTagSuggestion(value.Value!.Trim(), value.Confidence))
            .ToArray();
    }

    private static string SafeFileName(string fileName)
    {
        var safe = Path.GetFileName(fileName);
        return string.IsNullOrWhiteSpace(safe) ? "garment.png" : safe;
    }

    private static string TrimForError(string value)
    {
        value = value.Trim();
        return value.Length <= 800 ? value : value[..800];
    }

    private sealed record ClassifyResponseDto(
        string? Provider,
        ClassifyValueDto? Category,
        List<ClassifyColorDto>? Colors,
        List<ClassifyValueDto>? Seasons,
        List<ClassifyValueDto>? Tags);

    private sealed record ClassifyValueDto(string? Value, double Confidence);

    private sealed record ClassifyColorDto(string? Name, string? Hex, double Confidence);
}

public sealed class DisabledGarmentAutoTagger : IGarmentAutoTagger
{
    public string Name => "disabled";

    public GarmentAutoTagResult Classify(GarmentAutoTagRequest request) => GarmentAutoTagResult.Empty(Name);
}

public sealed class AutoGarmentAutoTagger : IGarmentAutoTagger
{
    private readonly IGarmentAutoTagger _preferred;
    private readonly IGarmentAutoTagger _fallback;
    private readonly Func<bool> _isPreferredHealthy;

    public AutoGarmentAutoTagger(IGarmentAutoTagger preferred, IGarmentAutoTagger fallback, Func<bool> isPreferredHealthy)
    {
        _preferred = preferred;
        _fallback = fallback;
        _isPreferredHealthy = isPreferredHealthy;
    }

    public string Name => "auto";

    public GarmentAutoTagResult Classify(GarmentAutoTagRequest request)
    {
        if (!_isPreferredHealthy())
        {
            return _fallback.Classify(request);
        }

        try
        {
            return _preferred.Classify(request);
        }
        catch
        {
            // Service went away mid-flight — degrade to no suggestions rather than error.
            return _fallback.Classify(request);
        }
    }
}

// Cheap cached reachability check for the local auto-tagging service. Probes the health
// endpoint at most once per cooldown window so Auto routing neither hammers a down
// service nor blocks each upload; it self-heals when the service comes up later.
public sealed class GarmentAutoTagHealthProbe
{
    private readonly HttpClient _client;
    private readonly string _healthEndpoint;
    private readonly long _cooldownMs;
    private readonly object _gate = new();
    private long _nextCheckTick;
    private bool _healthy;

    public GarmentAutoTagHealthProbe(HttpClient client, string healthEndpoint, TimeSpan cooldown)
    {
        _client = client;
        _healthEndpoint = healthEndpoint;
        _cooldownMs = (long)Math.Max(0, cooldown.TotalMilliseconds);
    }

    public bool IsHealthy()
    {
        if (string.IsNullOrWhiteSpace(_healthEndpoint))
        {
            return false;
        }

        lock (_gate)
        {
            var now = Environment.TickCount64;
            if (_nextCheckTick != 0 && now < _nextCheckTick)
            {
                return _healthy;
            }

            _healthy = Probe();
            _nextCheckTick = now + _cooldownMs;
            return _healthy;
        }
    }

    private bool Probe()
    {
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Get, _healthEndpoint);
            using var response = _client.Send(message);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}

// Produces a clean cutout from an original photo's bytes by reusing the existing garment
// extraction (background-removal) pipeline. No persistence.
public sealed class GarmentCutoutFactory : IGarmentCutoutFactory
{
    private readonly IGarmentExtractionProvider _extraction;

    public GarmentCutoutFactory(IGarmentExtractionProvider extraction)
    {
        _extraction = extraction;
    }

    public byte[]? CreateCutout(byte[] originalImageBytes)
    {
        if (originalImageBytes is not { Length: > 0 })
        {
            return null;
        }

        var result = _extraction.ExtractGarments(new GarmentExtractionRequest("garment.png", "image/png", originalImageBytes));
        var candidate = result.Items.Count > 0 ? result.Items[0] : null;
        return candidate?.ImageBytes;
    }
}
