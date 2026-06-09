using OutfitPlanner.Domain;

namespace OutfitPlanner.Application.Abstractions;

public sealed record TryOnGeneration(string ProviderJobId, string OutputImageUrl);

public sealed record TryOnOptions(bool SequentialFlowEnabled);

public interface ITryOnProvider
{
    string Name => GetType().Name;

    TryOnGeneration Generate(string userId, Outfit outfit, string bodyReferencePhotoUrl, TryOnOptions options);
}
