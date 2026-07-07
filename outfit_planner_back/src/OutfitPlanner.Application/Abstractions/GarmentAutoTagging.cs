using OutfitPlanner.Domain;

namespace OutfitPlanner.Application.Abstractions;

// Auto-tagging suggests garment metadata (category, colors, seasons, tags) on upload so
// the wardrobe queue can PREFILL it. Suggestions only: the user always overrides and
// their edits are never overwritten. The tagger runs a LOCAL model service; when it is
// disabled or unreachable it degrades to an empty (unavailable) result and the upload
// flow is unchanged.

public sealed record GarmentAutoTagRequest(
    string FileName,
    string ContentType,
    byte[] ImageBytes,
    // The account's existing wardrobe tags. Preferred when suggesting tags.
    IReadOnlyList<string> KnownTags);

public sealed record AutoTagColorSuggestion(string Name, string Hex, double Confidence);

public sealed record AutoTagSuggestion(string Value, double Confidence);

public sealed record GarmentAutoTagResult(
    // Null when no category cleared its confidence threshold.
    GarmentCategory? Category,
    double CategoryConfidence,
    IReadOnlyList<AutoTagColorSuggestion> Colors,
    IReadOnlyList<AutoTagSuggestion> Seasons,
    IReadOnlyList<AutoTagSuggestion> Tags,
    string ProviderName)
{
    // False when the tagger is disabled or unreachable (result carries no suggestions).
    public bool IsAvailable { get; init; } = true;

    public static GarmentAutoTagResult Empty(string providerName) => new(
        Category: null,
        CategoryConfidence: 0,
        Colors: Array.Empty<AutoTagColorSuggestion>(),
        Seasons: Array.Empty<AutoTagSuggestion>(),
        Tags: Array.Empty<AutoTagSuggestion>(),
        ProviderName: providerName)
    {
        IsAvailable = false,
    };
}

public interface IGarmentAutoTagger
{
    string Name { get; }

    GarmentAutoTagResult Classify(GarmentAutoTagRequest request);
}

// Produces a clean transparent cutout from an original garment photo's bytes WITHOUT
// persisting anything. Lets auto-tagging feed the tagger a clean cutout even before the
// async background-removal worker has produced one for a freshly uploaded photo.
// Implemented in Infrastructure over the existing garment extraction (rembg) provider.
public interface IGarmentCutoutFactory
{
    // Returns cutout PNG bytes, or null when a cutout could not be produced.
    byte[]? CreateCutout(byte[] originalImageBytes);
}
