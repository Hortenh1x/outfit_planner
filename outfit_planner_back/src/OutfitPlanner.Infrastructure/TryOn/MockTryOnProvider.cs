using OutfitPlanner.Application.Abstractions;
using OutfitPlanner.Domain;

namespace OutfitPlanner.Infrastructure.TryOn;

public sealed class MockTryOnProvider : ITryOnProvider
{
    public TryOnGeneration Generate(string userId, Outfit outfit, string bodyReferencePhotoUrl, TryOnOptions options)
    {
        var providerJobId = $"mock_{Guid.NewGuid():N}";
        var encodedOutfit = Uri.EscapeDataString(outfit.Name.ToLowerInvariant().Replace(' ', '-'));
        return new TryOnGeneration(providerJobId, $"/generated/try-on/{outfit.Id:N}-{encodedOutfit}.png");
    }
}
