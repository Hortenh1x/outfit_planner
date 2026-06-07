using OutfitPlanner.Application.Abstractions;
using OutfitPlanner.Application.Common;
using OutfitPlanner.Domain;

namespace OutfitPlanner.Application.Services;

public sealed class OutfitService
{
    private readonly IGarmentRepository _garments;
    private readonly IOutfitRepository _outfits;
    private readonly IClock _clock;

    public OutfitService(IGarmentRepository garments, IOutfitRepository outfits, IClock clock)
    {
        _garments = garments;
        _outfits = outfits;
        _clock = clock;
    }

    public Outfit CreateOutfit(string userId, string name, IEnumerable<Guid> garmentIds)
    {
        var normalizedUserId = InputGuard.NormalizeUserId(userId);
        var selectedIds = garmentIds.Distinct().ToList();
        var selectedGarments = selectedIds
            .Select(id => _garments.GetGarmentByUser(normalizedUserId, id) ?? throw new InvalidOperationException($"Garment {id} was not found."))
            .ToList();

        var outfitItems = OutfitRules.BuildItems(selectedGarments);
        var outfit = new Outfit(
            Guid.NewGuid(),
            normalizedUserId,
            InputGuard.RequireText(name, "Outfit name"),
            outfitItems,
            BuildClothesOnlyPreview(outfitItems),
            null,
            _clock.UtcNow);

        _outfits.AddOutfit(outfit);
        return outfit;
    }

    public IReadOnlyList<Outfit> ListOutfits(string userId)
    {
        return _outfits.ListOutfitsByUser(InputGuard.NormalizeUserId(userId));
    }

    public Outfit? GetOutfit(string userId, Guid outfitId)
    {
        return _outfits.GetOutfitByUser(InputGuard.NormalizeUserId(userId), outfitId);
    }

    private static string BuildClothesOnlyPreview(IReadOnlyList<OutfitItem> outfitItems)
    {
        var itemIds = string.Join("-", outfitItems.Select(item => item.GarmentId.ToString("N")[..8]));
        return $"/generated/clothes-only/{itemIds}.png";
    }
}
