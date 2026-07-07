namespace OutfitPlanner.Domain;

public static class OutfitRules
{
    private static readonly BodyZone[] PresentationOrder =
    {
        BodyZone.FullBody,
        BodyZone.Torso,
        BodyZone.Legs,
        BodyZone.OuterLayer,
        BodyZone.Feet,
        BodyZone.Head,
        BodyZone.Hands,
        BodyZone.Accessory
    };

    private static readonly HashSet<BodyZone> ExclusiveZones = new()
    {
        BodyZone.Torso,
        BodyZone.Legs,
        BodyZone.FullBody,
        BodyZone.Feet,
        BodyZone.Head,
        BodyZone.Hands,
        BodyZone.OuterLayer
    };

    public static IReadOnlyList<OutfitItem> BuildItems(IEnumerable<GarmentItem> garments)
    {
        var items = garments.ToList();

        if (items.Count == 0)
        {
            throw new InvalidOperationException("An outfit needs at least one garment.");
        }

        ValidateSlotCompatibility(items);

        return items
            .OrderBy(item => Array.IndexOf(PresentationOrder, item.BodyZone))
            .ThenBy(item => item.Category)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(item => new OutfitItem(
                item.Id,
                item.Name,
                item.Category,
                item.BodyZone,
                item.ThumbnailUrl,
                item.RotationDegrees,
                item.CutoutWidthPx,
                item.CutoutHeightPx))
            .ToList();
    }

    private static void ValidateSlotCompatibility(IReadOnlyCollection<GarmentItem> items)
    {
        var fullBody = items.FirstOrDefault(item => item.BodyZone == BodyZone.FullBody);
        if (fullBody is not null)
        {
            var conflictingBase = items.FirstOrDefault(item => item.BodyZone is BodyZone.Torso or BodyZone.Legs);
            if (conflictingBase is not null)
            {
                throw new InvalidOperationException($"{fullBody.Category} cannot be combined with {conflictingBase.Category} because it occupies the full body slot.");
            }
        }

        foreach (var zone in ExclusiveZones)
        {
            var duplicate = items
                .Where(item => item.BodyZone == zone)
                .Take(2)
                .ToList();

            if (duplicate.Count > 1)
            {
                var slotName = zone == BodyZone.Legs ? "Bottom" : zone.ToString();
                throw new InvalidOperationException($"Only one garment can occupy the {slotName} slot unless explicit layering support is added.");
            }
        }
    }
}
