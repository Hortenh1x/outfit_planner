namespace OutfitPlanner.Application.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
