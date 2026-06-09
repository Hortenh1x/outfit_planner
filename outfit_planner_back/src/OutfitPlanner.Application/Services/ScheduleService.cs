using OutfitPlanner.Application.Abstractions;
using OutfitPlanner.Application.Common;
using OutfitPlanner.Domain;

namespace OutfitPlanner.Application.Services;

public sealed class ScheduleService
{
    private readonly IOutfitRepository _outfits;
    private readonly IOutfitScheduleRepository _schedule;
    private readonly IClock _clock;

    public ScheduleService(IOutfitRepository outfits, IOutfitScheduleRepository schedule, IClock clock)
    {
        _outfits = outfits;
        _schedule = schedule;
        _clock = clock;
    }

    public ScheduledOutfit ScheduleOutfit(string userId, DateOnly date, Guid outfitId)
    {
        var normalizedUserId = InputGuard.NormalizeUserId(userId);
        if (_outfits.GetOutfitByUser(normalizedUserId, outfitId) is null)
        {
            throw new InvalidOperationException("Outfit was not found.");
        }

        var scheduled = new ScheduledOutfit(Guid.NewGuid(), normalizedUserId, date, outfitId, _clock.UtcNow);
        _schedule.UpsertScheduledOutfit(scheduled);
        return scheduled;
    }

    public IReadOnlyList<ScheduledOutfit> GetSchedule(string userId, DateOnly from, DateOnly to)
    {
        if (to < from)
        {
            throw new InvalidOperationException("Schedule range end must be on or after range start.");
        }

        return _schedule.ListScheduleByUser(InputGuard.NormalizeUserId(userId), from, to);
    }

    public bool UnscheduleOutfit(string userId, DateOnly date)
    {
        return _schedule.DeleteScheduledOutfitByUserDate(InputGuard.NormalizeUserId(userId), date);
    }
}
