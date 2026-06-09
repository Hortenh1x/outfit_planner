using OutfitPlanner.Application.Abstractions;
using OutfitPlanner.Application.Common;
using OutfitPlanner.Domain;

namespace OutfitPlanner.Application.Services;

public sealed class OutfitService
{
    private const int MaxListLimit = 100;

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
            Array.Empty<string>(),
            Array.Empty<string>(),
            false,
            false,
            BuildClothesOnlyPreview(outfitItems),
            null,
            _clock.UtcNow);

        _outfits.AddOutfit(outfit);
        return outfit;
    }

    public IReadOnlyList<Outfit> ListOutfits(string userId)
    {
        return ListOutfits(userId, new OutfitQuery());
    }

    public IReadOnlyList<Outfit> ListOutfits(string userId, OutfitQuery query)
    {
        return _outfits.ListOutfitsByUser(InputGuard.NormalizeUserId(userId), NormalizeQuery(query));
    }

    public Outfit? GetOutfit(string userId, Guid outfitId)
    {
        return _outfits.GetOutfitByUser(InputGuard.NormalizeUserId(userId), outfitId);
    }

    public Outfit? UpdateOutfit(string userId, Guid outfitId, UpdateOutfitCommand command)
    {
        var normalizedUserId = InputGuard.NormalizeUserId(userId);
        var existing = _outfits.GetOutfitByUser(normalizedUserId, outfitId);
        if (existing is null)
        {
            return null;
        }

        var items = existing.Items;
        var clothesOnlyPreviewUrl = existing.ClothesOnlyPreviewUrl;
        var personPreviewUrl = existing.PersonPreviewUrl;
        if (command.GarmentIds is not null)
        {
            var selectedIds = command.GarmentIds.Distinct().ToList();
            var selectedGarments = selectedIds
                .Select(id => _garments.GetGarmentByUser(normalizedUserId, id) ?? throw new InvalidOperationException($"Garment {id} was not found."))
                .ToList();

            items = OutfitRules.BuildItems(selectedGarments);
            clothesOnlyPreviewUrl = BuildClothesOnlyPreview(items);
            personPreviewUrl = null;
        }

        var updated = existing with
        {
            Name = command.Name is null ? existing.Name : InputGuard.RequireText(command.Name, "Outfit name"),
            Items = items,
            Tags = command.Tags is null ? existing.Tags : NormalizeTags(command.Tags),
            Occasion = command.Occasion is null ? existing.Occasion : NormalizeTokens(command.Occasion),
            IsFavorite = command.IsFavorite ?? existing.IsFavorite,
            IsArchived = command.IsArchived ?? existing.IsArchived,
            ClothesOnlyPreviewUrl = clothesOnlyPreviewUrl,
            PersonPreviewUrl = personPreviewUrl
        };

        _outfits.UpdateOutfit(updated);
        return updated;
    }

    public bool DeleteOutfit(string userId, Guid outfitId)
    {
        return _outfits.DeleteOutfitByUser(InputGuard.NormalizeUserId(userId), outfitId);
    }

    private static string BuildClothesOnlyPreview(IReadOnlyList<OutfitItem> outfitItems)
    {
        var itemIds = string.Join("-", outfitItems.Select(item => item.GarmentId.ToString("N")[..8]));
        return $"/generated/clothes-only/{itemIds}.png";
    }

    private static OutfitQuery NormalizeQuery(OutfitQuery query)
    {
        return query with
        {
            Search = NormalizeOptionalText(query.Search),
            Occasion = NormalizeToken(query.Occasion),
            Sort = NormalizeToken(query.Sort),
            Offset = query.Offset is null ? null : Math.Max(0, query.Offset.Value),
            Limit = query.Limit is null ? null : Math.Clamp(query.Limit.Value, 1, MaxListLimit)
        };
    }

    private static IReadOnlyList<string> NormalizeTags(IReadOnlyList<string> tags)
    {
        return tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList();
    }

    private static IReadOnlyList<string> NormalizeTokens(IReadOnlyList<string> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList();
    }

    private static string? NormalizeToken(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
