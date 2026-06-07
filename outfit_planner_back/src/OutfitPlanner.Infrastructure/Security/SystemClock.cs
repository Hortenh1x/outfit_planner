using OutfitPlanner.Application.Abstractions;

namespace OutfitPlanner.Infrastructure.Security;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
