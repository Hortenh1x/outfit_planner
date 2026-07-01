using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OutfitPlanner.Application.Abstractions;
using OutfitPlanner.Domain;

namespace OutfitPlanner.Infrastructure.TryOn;

public sealed record FashnTryOnSettings(
    string ApiKey,
    string ModelName,
    string Mode,
    int MaxPollingAttempts,
    TimeSpan PollInterval,
    int NumSamples,
    string OutputFormat,
    bool ReturnBase64,
    bool SegmentationFree,
    string GarmentPhotoType,
    int? Seed,
    string Resolution = "1k",
    string? GenderPromptTemplate = null)
{
    public bool UsesTryOnMax => string.Equals(ModelName, "tryon-max", StringComparison.OrdinalIgnoreCase);

    public int CreditsPerRun => UsesTryOnMax && string.Equals(Mode, "quality", StringComparison.OrdinalIgnoreCase) && string.Equals(Resolution, "4k", StringComparison.OrdinalIgnoreCase)
        ? 5
        : UsesTryOnMax
            ? 2
            : 1;

    public string SettingsHash => UsesTryOnMax
        ? $"{ModelName}:{Mode}:{Resolution}:{OutputFormat}:{GenderPromptTemplate}"
        : $"{ModelName}:{Mode}";
}

public sealed class FashnTryOnProvider : ITryOnProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly FashnTryOnSettings _settings;

    public string Name => nameof(FashnTryOnProvider);

    public TryOnProviderCapabilities Capabilities => new(
        Name,
        _settings.ModelName,
        _settings.Mode,
        _settings.SettingsHash,
        new HashSet<TryOnMode>
        {
            TryOnMode.SingleGarmentTryOn,
            TryOnMode.SequentialOutfitTryOn
        })
    {
        CreditsPerRun = _settings.CreditsPerRun
    };

    public FashnTryOnProvider(HttpClient http, FashnTryOnSettings settings)
    {
        _http = http;
        _settings = settings;
    }

    public TryOnGeneration Generate(TryOnProviderRequest request)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            throw new InvalidOperationException("FASHN API key is not configured.");
        }

        if (!Capabilities.SupportedModes.Contains(request.Mode))
        {
            throw new InvalidOperationException($"FASHN does not support {request.Mode}.");
        }

        if (request.BodyTryOnItems.Count == 0)
        {
            throw new InvalidOperationException("At least one garment is required for FASHN try-on.");
        }

        if (request.Mode == TryOnMode.SingleGarmentTryOn && request.BodyTryOnItems.Count != 1)
        {
            throw new InvalidOperationException("Enable sequential flow before sending a multi-garment outfit to FASHN.");
        }

        return GenerateSequentially(request.BodyReferencePhotoUrl, request.BodyTryOnItems, request.UserGender);
    }

    private TryOnGeneration GenerateSequentially(string bodyReferencePhotoUrl, IReadOnlyList<OutfitItem> items, UserGender? userGender)
    {
        var modelImage = bodyReferencePhotoUrl;
        TryOnGeneration? latest = null;

        foreach (var item in items)
        {
            var predictionId = SubmitPrediction(modelImage, item, userGender);
            latest = PollUntilCompleted(predictionId);
            modelImage = latest.OutputImageUrl;
        }

        return latest ?? throw new InvalidOperationException("FASHN generation did not produce an output.");
    }

    private string SubmitPrediction(string bodyReferencePhotoUrl, OutfitItem item, UserGender? userGender)
    {
        var content = _settings.UsesTryOnMax
            ? JsonContent(new FashnTryOnMaxRunRequest(
                _settings.ModelName,
                new FashnTryOnMaxInputs(
                    bodyReferencePhotoUrl,
                    item.ThumbnailUrl,
                    GenderPrompt(userGender),
                    _settings.Resolution,
                    _settings.Mode,
                    _settings.NumSamples,
                    _settings.OutputFormat,
                    _settings.ReturnBase64,
                    _settings.Seed)))
            : JsonContent(new FashnRunRequest(
                _settings.ModelName,
                new FashnTryOnInputs(
                    bodyReferencePhotoUrl,
                    item.ThumbnailUrl,
                    FashnCategory(item.Category),
                    _settings.Mode,
                    _settings.NumSamples,
                    _settings.OutputFormat,
                    _settings.ReturnBase64,
                    _settings.SegmentationFree,
                    _settings.GarmentPhotoType,
                    _settings.Seed)));

        using var request = new HttpRequestMessage(HttpMethod.Post, "run")
        {
            Content = content
        };
        AddAuth(request);

        using var response = _http.SendAsync(request).GetAwaiter().GetResult();
        var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"FASHN run request failed with {(int)response.StatusCode}: {ExtractError(body)}");
        }

        var run = JsonSerializer.Deserialize<FashnRunResponse>(body, JsonOptions)
            ?? throw new InvalidOperationException("FASHN run response was empty.");
        if (!string.IsNullOrWhiteSpace(run.Error))
        {
            throw new InvalidOperationException($"FASHN run request failed: {run.Error}");
        }

        return string.IsNullOrWhiteSpace(run.Id)
            ? throw new InvalidOperationException("FASHN run response did not include prediction id.")
            : run.Id;
    }

    private TryOnGeneration PollUntilCompleted(string predictionId)
    {
        for (var attempt = 0; attempt < _settings.MaxPollingAttempts; attempt++)
        {
            if (_settings.PollInterval > TimeSpan.Zero)
            {
                Thread.Sleep(_settings.PollInterval);
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, $"status/{Uri.EscapeDataString(predictionId)}");
            AddAuth(request);

            using var response = _http.SendAsync(request).GetAwaiter().GetResult();
            var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"FASHN status request failed with {(int)response.StatusCode}: {ExtractError(body)}");
            }

            var status = JsonSerializer.Deserialize<FashnStatusResponse>(body, JsonOptions)
                ?? throw new InvalidOperationException("FASHN status response was empty.");
            if (string.Equals(status.Status, "completed", StringComparison.OrdinalIgnoreCase))
            {
                var output = status.Output?.FirstOrDefault();
                return string.IsNullOrWhiteSpace(output)
                    ? throw new InvalidOperationException("FASHN completed without an output image URL.")
                    : new TryOnGeneration(predictionId, output);
            }

            if (string.Equals(status.Status, "failed", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"FASHN generation failed: {status.Error?.Message ?? status.Error?.Name ?? "Unknown error"}");
            }
        }

        throw new InvalidOperationException("FASHN generation did not complete before polling timed out.");
    }

    private void AddAuth(HttpRequestMessage request)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
    }

    private static StringContent JsonContent<T>(T value)
    {
        return new StringContent(JsonSerializer.Serialize(value, JsonOptions), Encoding.UTF8, "application/json");
    }

    private static string FashnCategory(GarmentCategory category)
    {
        return category switch
        {
            GarmentCategory.Top => "tops",
            GarmentCategory.Bottom => "bottoms",
            _ => "auto"
        };
    }

    private string? GenderPrompt(UserGender? userGender)
    {
        if (string.IsNullOrWhiteSpace(_settings.GenderPromptTemplate))
        {
            return null;
        }

        if (userGender is null)
        {
            return null;
        }

        var gender = userGender == UserGender.Male ? "male" : "female";
        var template = _settings.GenderPromptTemplate.Trim();
        return template
            .Replace("{gender}", gender, StringComparison.OrdinalIgnoreCase)
            .Replace("{Gender}", char.ToUpperInvariant(gender[0]) + gender[1..], StringComparison.Ordinal);
    }

    private static string ExtractError(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return "empty response";
        }

        try
        {
            using var json = JsonDocument.Parse(body);
            if (json.RootElement.TryGetProperty("message", out var message))
            {
                return message.GetString() ?? body;
            }

            if (json.RootElement.TryGetProperty("error", out var error))
            {
                return error.ValueKind == JsonValueKind.String ? error.GetString() ?? body : error.ToString();
            }
        }
        catch (JsonException)
        {
            return body;
        }

        return body;
    }
}

file sealed record FashnRunRequest(
    [property: JsonPropertyName("model_name")] string ModelName,
    [property: JsonPropertyName("inputs")] FashnTryOnInputs Inputs);

file sealed record FashnTryOnMaxRunRequest(
    [property: JsonPropertyName("model_name")] string ModelName,
    [property: JsonPropertyName("inputs")] FashnTryOnMaxInputs Inputs);

file sealed record FashnTryOnInputs(
    [property: JsonPropertyName("model_image")] string ModelImage,
    [property: JsonPropertyName("garment_image")] string GarmentImage,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("mode")] string Mode,
    [property: JsonPropertyName("num_samples")] int NumSamples,
    [property: JsonPropertyName("output_format")] string OutputFormat,
    [property: JsonPropertyName("return_base64")] bool ReturnBase64,
    [property: JsonPropertyName("segmentation_free")] bool SegmentationFree,
    [property: JsonPropertyName("garment_photo_type")] string GarmentPhotoType,
    [property: JsonPropertyName("seed")] int? Seed);

file sealed record FashnTryOnMaxInputs(
    [property: JsonPropertyName("model_image")] string ModelImage,
    [property: JsonPropertyName("product_image")] string ProductImage,
    [property: JsonPropertyName("prompt")] string? Prompt,
    [property: JsonPropertyName("resolution")] string Resolution,
    [property: JsonPropertyName("generation_mode")] string GenerationMode,
    [property: JsonPropertyName("num_images")] int NumImages,
    [property: JsonPropertyName("output_format")] string OutputFormat,
    [property: JsonPropertyName("return_base64")] bool ReturnBase64,
    [property: JsonPropertyName("seed")] int? Seed);

file sealed record FashnRunResponse(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("error")] string? Error);

file sealed record FashnStatusResponse(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("output")] IReadOnlyList<string>? Output,
    [property: JsonPropertyName("error")] FashnError? Error);

file sealed record FashnError(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("message")] string? Message);
