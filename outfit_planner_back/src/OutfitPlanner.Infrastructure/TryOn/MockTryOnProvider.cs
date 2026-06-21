using OutfitPlanner.Application.Abstractions;
using OutfitPlanner.Domain;

namespace OutfitPlanner.Infrastructure.TryOn;

public sealed class MockTryOnProvider : ITryOnProvider
{
    public string Name => nameof(MockTryOnProvider);

    public TryOnProviderCapabilities Capabilities => new(
        Name,
        "mock",
        "mock",
        "mock",
        new HashSet<TryOnMode>
        {
            TryOnMode.ClothesOnlyPreview,
            TryOnMode.SingleGarmentTryOn,
            TryOnMode.SequentialOutfitTryOn,
            TryOnMode.ExperimentalCompositeTryOn
        });

    public TryOnGeneration Generate(TryOnProviderRequest request)
    {
        var providerJobId = $"mock_{Guid.NewGuid():N}";
        var encodedMode = Uri.EscapeDataString(request.Mode.ToString().ToLowerInvariant());
        return new TryOnGeneration(providerJobId, $"/generated/try-on/{request.OutfitId:N}-{encodedMode}.png");
    }
}
