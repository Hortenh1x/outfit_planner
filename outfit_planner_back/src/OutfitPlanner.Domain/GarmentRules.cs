namespace OutfitPlanner.Domain;

public static class GarmentRules
{
    public static BodyZone GetBodyZone(GarmentCategory category)
    {
        return category switch
        {
            GarmentCategory.Top => BodyZone.Torso,
            GarmentCategory.Bottom => BodyZone.Legs,
            _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Unsupported garment category.")
        };
    }
}
