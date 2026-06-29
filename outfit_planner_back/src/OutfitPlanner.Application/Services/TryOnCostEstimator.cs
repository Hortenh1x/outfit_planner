using System.Security.Cryptography;
using System.Text;
using OutfitPlanner.Domain;

namespace OutfitPlanner.Application.Services;

public sealed record TryOnEstimateInput
{
    public TryOnEstimateInput(TryOnMode mode, string providerName, string bodyReferenceIdentity, string settingsHash, bool hasCachedResult, int creditsPerRun = 1, UserGender? userGender = null)
    {
        Mode = mode;
        ProviderName = providerName;
        BodyReferenceIdentity = bodyReferenceIdentity;
        SettingsHash = settingsHash;
        HasCachedResult = hasCachedResult;
        CreditsPerRun = Math.Max(1, creditsPerRun);
        UserGender = userGender;
    }

    public TryOnMode Mode { get; }
    public string ProviderName { get; }
    public string BodyReferenceIdentity { get; }
    public string SettingsHash { get; }
    public bool HasCachedResult { get; }
    public int CreditsPerRun { get; }
    public UserGender? UserGender { get; }
}

public sealed record TryOnCostEstimate(
    TryOnMode Mode,
    string ProviderName,
    IReadOnlyList<OutfitItem> BodyTryOnItems,
    IReadOnlyList<OutfitItem> VisualOnlyItems,
    IReadOnlyList<Guid> IncludedGarmentIds,
    IReadOnlyList<Guid> ExcludedGarmentIds,
    int EstimatedCredits,
    bool IsAvailable,
    bool RequiresAi,
    bool RequiresPremiumConfirmation,
    string CacheKey,
    bool HasCachedResult,
    string Summary,
    IReadOnlyList<string> Warnings);

public sealed class TryOnCostEstimator
{
    private static readonly HashSet<GarmentCategory> BodyTryOnCategories = new()
    {
        GarmentCategory.Top,
        GarmentCategory.Bottom,
        GarmentCategory.Dress,
        GarmentCategory.Outerwear
    };

    public TryOnCostEstimate Estimate(Outfit outfit, TryOnEstimateInput input)
    {
        var bodyItems = outfit.Items.Where(item => BodyTryOnCategories.Contains(item.Category)).ToList();
        var visualItems = outfit.Items.Where(item => !BodyTryOnCategories.Contains(item.Category)).ToList();
        var warnings = new List<string>();
        var included = IncludedItems(input.Mode, bodyItems, visualItems);
        var excluded = outfit.Items
            .Where(item => !included.Any(includedItem => includedItem.GarmentId == item.GarmentId))
            .ToList();
        var isAvailable = true;
        var summary = "Ready to estimate try-on generation.";

        if (input.Mode == TryOnMode.SingleGarmentTryOn && bodyItems.Count != 1)
        {
            isAvailable = false;
            summary = "Single garment try-on requires exactly one body garment.";
        }

        if (input.Mode is TryOnMode.SingleGarmentTryOn or TryOnMode.SequentialOutfitTryOn && bodyItems.Count == 0)
        {
            isAvailable = false;
            summary = "Paid try-on requires at least one top, bottom, dress, or outerwear item.";
            warnings.Add("This outfit has only visual-only items. Use ClothesOnlyPreview for a free preview.");
        }

        if (input.Mode == TryOnMode.SequentialOutfitTryOn && bodyItems.Count > 0)
        {
            summary = $"Sequential outfit try-on will use {bodyItems.Count} body garment run(s).";
        }

        if (input.Mode == TryOnMode.ExperimentalCompositeTryOn)
        {
            summary = "Composite premium try-on will send one composed outfit reference to AI.";
        }

        if (visualItems.Count > 0 && input.Mode != TryOnMode.ExperimentalCompositeTryOn)
        {
            warnings.Add("Shoes, bags, accessories, and hats are visual-only and will not be sent to AI in this mode.");
        }

        var credits = input.Mode switch
        {
            TryOnMode.ClothesOnlyPreview => 0,
            TryOnMode.SingleGarmentTryOn => input.CreditsPerRun,
            TryOnMode.SequentialOutfitTryOn => bodyItems.Count * input.CreditsPerRun,
            TryOnMode.ExperimentalCompositeTryOn => input.CreditsPerRun,
            _ => throw new InvalidOperationException($"Unsupported try-on mode {input.Mode}.")
        };
        var garmentRotations = included.ToDictionary(item => item.GarmentId, item => item.RotationDegrees);
        var cacheKey = BuildCacheKey(input.BodyReferenceIdentity, included.Select(item => item.GarmentId), input.ProviderName, input.Mode, input.SettingsHash, input.UserGender, garmentRotations);

        return new TryOnCostEstimate(
            input.Mode,
            input.ProviderName,
            bodyItems,
            visualItems,
            included.Select(item => item.GarmentId).OrderBy(id => id).ToArray(),
            excluded.Select(item => item.GarmentId).OrderBy(id => id).ToArray(),
            credits,
            isAvailable,
            input.Mode != TryOnMode.ClothesOnlyPreview,
            input.Mode == TryOnMode.ExperimentalCompositeTryOn,
            cacheKey,
            input.HasCachedResult,
            summary,
            warnings);
    }

    public static string BuildCacheKey(string bodyReferenceIdentity, IEnumerable<Guid> garmentIds, string providerName, TryOnMode mode, string settingsHash, UserGender? userGender = null, IReadOnlyDictionary<Guid, double>? garmentRotations = null)
    {
        var sortedGarments = string.Join(",", garmentIds.OrderBy(id => id).Select(id =>
        {
            var rotation = garmentRotations is not null && garmentRotations.TryGetValue(id, out var degrees) ? degrees : 0d;
            return $"{id:N}@{Math.Round(rotation, 2).ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        }));
        var raw = $"tryon:v3|body={bodyReferenceIdentity}|garments={sortedGarments}|provider={providerName}|mode={mode}|settings={settingsHash}|gender={userGender?.ToString() ?? "unspecified"}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static IReadOnlyList<OutfitItem> IncludedItems(TryOnMode mode, IReadOnlyList<OutfitItem> bodyItems, IReadOnlyList<OutfitItem> visualItems)
    {
        return mode switch
        {
            TryOnMode.ClothesOnlyPreview => Array.Empty<OutfitItem>(),
            TryOnMode.SingleGarmentTryOn => bodyItems.Count == 1 ? bodyItems : Array.Empty<OutfitItem>(),
            TryOnMode.SequentialOutfitTryOn => bodyItems,
            TryOnMode.ExperimentalCompositeTryOn => bodyItems.Concat(visualItems).ToList(),
            _ => throw new InvalidOperationException($"Unsupported try-on mode {mode}.")
        };
    }
}
