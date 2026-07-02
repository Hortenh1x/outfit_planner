namespace OutfitPlanner.Domain;

public static class GarmentRules
{
    public static BodyZone GetBodyZone(GarmentCategory category)
    {
        return category switch
        {
            GarmentCategory.Top => BodyZone.Torso,
            GarmentCategory.Bottom => BodyZone.Legs,
            GarmentCategory.Dress => BodyZone.FullBody,
            GarmentCategory.Outerwear => BodyZone.OuterLayer,
            GarmentCategory.Shoes => BodyZone.Feet,
            GarmentCategory.Bag => BodyZone.Accessory,
            GarmentCategory.Accessory => BodyZone.Accessory,
            _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Unsupported garment category.")
        };
    }
}
