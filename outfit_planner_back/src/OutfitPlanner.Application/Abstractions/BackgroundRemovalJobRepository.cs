using OutfitPlanner.Domain;

namespace OutfitPlanner.Application.Abstractions;

public interface IBackgroundRemovalJobRepository
{
    void AddBackgroundRemovalJob(BackgroundRemovalJob job);

    BackgroundRemovalJob? GetBackgroundRemovalJobById(Guid jobId);

    BackgroundRemovalJob? GetBackgroundRemovalJobByUser(string userId, Guid jobId);

    void UpdateBackgroundRemovalJob(BackgroundRemovalJob job);

    /// <summary>Jobs still Pending or Processing (used to re-enqueue after a restart).</summary>
    IReadOnlyList<BackgroundRemovalJob> ListUnfinishedBackgroundRemovalJobs();
}
