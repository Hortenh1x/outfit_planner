namespace OutfitPlanner.Domain;

public static class OutfitRules
{
    public static IReadOnlyList<OutfitItem> BuildItems(IEnumerable<GarmentItem> garments)
    {
        var items = garments.ToList();

        if (items.Count == 0)
        {
            throw new InvalidOperationException("An outfit needs at least one garment.");
        }

        var duplicateCategory = items
            .GroupBy(item => item.Category)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateCategory is not null)
        {
            throw new InvalidOperationException($"Only one {duplicateCategory.Key} garment can be used in an outfit.");
        }

        return items
            .OrderBy(item => item.Category)
            .Select(item => new OutfitItem(item.Id, item.Name, item.Category, item.BodyZone, item.ThumbnailUrl))
            .ToList();
    }
}
