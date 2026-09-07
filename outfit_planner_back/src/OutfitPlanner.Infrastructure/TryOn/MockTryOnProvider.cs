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

    // The mock never calls an AI service. Its output is a `mock:` URL that TryOnOutputStorage
    // turns into a real placeholder image in app-owned object storage, so the Builder, cards,
    // dialog and share page render an actual picture (labelled as a mock) instead of a broken
    // <img> pointing at a path nothing serves.
    public const string OutputScheme = "mock";

    public TryOnGeneration Generate(TryOnProviderRequest request)
    {
        var providerJobId = $"mock_{Guid.NewGuid():N}";
        var encodedMode = Uri.EscapeDataString(request.Mode.ToString().ToLowerInvariant());
        return new TryOnGeneration(providerJobId, $"{OutputScheme}://try-on/{request.OutfitId:N}-{encodedMode}.png");
    }
}
