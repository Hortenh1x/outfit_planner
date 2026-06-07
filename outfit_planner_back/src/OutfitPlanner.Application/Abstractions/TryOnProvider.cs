using OutfitPlanner.Domain;

namespace OutfitPlanner.Application.Abstractions;

public sealed record TryOnGeneration(string ProviderJobId, string OutputImageUrl);

public sealed record TryOnOptions(bool SequentialFlowEnabled);

public interface ITryOnProvider
{
    TryOnGeneration Generate(string userId, Outfit outfit, string bodyReferencePhotoUrl, TryOnOptions options);
}
