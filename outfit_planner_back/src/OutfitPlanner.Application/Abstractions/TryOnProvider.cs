using OutfitPlanner.Domain;

namespace OutfitPlanner.Application.Abstractions;

public sealed record TryOnGeneration(string ProviderJobId, string OutputImageUrl);

public sealed record TryOnGenerationSettings(string ModelName, string Mode, string SettingsHash);

public sealed record TryOnProviderRequest(
    string UserId,
    Guid OutfitId,
    TryOnMode Mode,
    string BodyReferencePhotoUrl,
    IReadOnlyList<OutfitItem> BodyTryOnItems,
    IReadOnlyList<OutfitItem> VisualOnlyItems,
    TryOnGenerationSettings Settings)
{
    public UserGender? UserGender { get; init; }
}

public sealed record TryOnProviderCapabilities(
    string ProviderName,
    string ModelName,
    string ProviderMode,
    string SettingsHash,
    IReadOnlySet<TryOnMode> SupportedModes)
{
    public int CreditsPerRun { get; init; } = 1;
}

public sealed record TryOnOptions(bool SequentialFlowEnabled);

public interface ITryOnProvider
{
    string Name => GetType().Name;

    TryOnProviderCapabilities Capabilities => new(
        Name,
        "default",
        "default",
        "default",
        new HashSet<TryOnMode>
        {
            TryOnMode.ClothesOnlyPreview,
            TryOnMode.SingleGarmentTryOn,
            TryOnMode.SequentialOutfitTryOn,
            TryOnMode.ExperimentalCompositeTryOn
        });

    TryOnGeneration Generate(TryOnProviderRequest request);

    TryOnGeneration Generate(string userId, Outfit outfit, string bodyReferencePhotoUrl, TryOnOptions options)
    {
        var mode = options.SequentialFlowEnabled ? TryOnMode.SequentialOutfitTryOn : TryOnMode.SingleGarmentTryOn;
        var bodyItems = outfit.Items
            .Where(item => item.Category is GarmentCategory.Top or GarmentCategory.Bottom or GarmentCategory.Dress or GarmentCategory.Outerwear)
            .ToArray();
        var visualItems = outfit.Items
            .Where(item => item.Category is GarmentCategory.Shoes or GarmentCategory.Bag or GarmentCategory.Accessory or GarmentCategory.Hat)
            .ToArray();
        return Generate(new TryOnProviderRequest(
            userId,
            outfit.Id,
            mode,
            bodyReferencePhotoUrl,
            bodyItems,
            visualItems,
            new TryOnGenerationSettings(
                Capabilities.ModelName,
                Capabilities.ProviderMode,
                Capabilities.SettingsHash)));
    }
}
