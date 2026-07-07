using OutfitPlanner.Application.Abstractions;

namespace OutfitPlanner.Application.Services;

// Orchestrates garment auto-tag classification for an uploaded photo. Resolves a clean
// cutout for the garment behind an upload-response image URL, then asks the tagger for
// prefill suggestions. NEVER throws: any failure (unresolvable image, tagger down)
// yields an empty (unavailable) result so the upload experience is never degraded.
public sealed class GarmentAutoTagService
{
    private readonly IGarmentAutoTagger _tagger;
    private readonly IGarmentCutoutImageReader _cutoutReader;
    private readonly IGarmentOriginalImageReader _originalReader;
    private readonly IGarmentCutoutFactory _cutoutFactory;

    public GarmentAutoTagService(
        IGarmentAutoTagger tagger,
        IGarmentCutoutImageReader cutoutReader,
        IGarmentOriginalImageReader originalReader,
        IGarmentCutoutFactory cutoutFactory)
    {
        _tagger = tagger;
        _cutoutReader = cutoutReader;
        _originalReader = originalReader;
        _cutoutFactory = cutoutFactory;
    }

    public GarmentAutoTagResult Classify(string imageUrl, IReadOnlyList<string>? knownTags)
    {
        try
        {
            var bytes = ResolveCleanImageBytes(imageUrl);
            if (bytes is not { Length: > 0 })
            {
                return GarmentAutoTagResult.Empty(_tagger.Name);
            }

            return _tagger.Classify(new GarmentAutoTagRequest(
                "garment-cutout.png",
                "image/png",
                bytes,
                knownTags ?? Array.Empty<string>()));
        }
        catch
        {
            // Prefill is best-effort; a classification failure must not surface as an error.
            return GarmentAutoTagResult.Empty(_tagger.Name);
        }
    }

    // Prefers an already-processed transparent cutout (clean input, no extra work). When
    // none exists yet (the fast-path upload flow defers background removal), makes one
    // from the stored original so the tagger always sees a clean cutout. Falls back to
    // the raw original if a cutout cannot be produced.
    private byte[]? ResolveCleanImageBytes(string imageUrl)
    {
        var cutout = _cutoutReader.ReadGarmentCutoutImageBytes(imageUrl);
        if (cutout is { Length: > 0 })
        {
            return cutout;
        }

        var original = _originalReader.ReadGarmentOriginalImageBytes(imageUrl);
        if (original is not { Length: > 0 })
        {
            return null;
        }

        var produced = _cutoutFactory.CreateCutout(original);
        return produced is { Length: > 0 } ? produced : original;
    }
}
