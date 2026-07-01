namespace OutfitPlanner.Application.Abstractions;

/// <summary>
/// Processes one queued background-removal job: loads the original, runs rembg, applies clothing
/// auto-straighten, stores the cutout, and updates the job (and its linked garment, if any).
/// Implemented in the integration phase; the hosted worker depends only on this seam.
/// </summary>
public interface IBackgroundRemovalJobProcessor
{
    Task ProcessAsync(Guid jobId, CancellationToken cancellationToken);
}
