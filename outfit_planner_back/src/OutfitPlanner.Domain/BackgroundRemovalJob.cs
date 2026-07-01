namespace OutfitPlanner.Domain;

public enum BackgroundRemovalStatus
{
    Pending,
    Processing,
    Succeeded,
    Failed
}

/// <summary>
/// An asynchronous background-removal task for an uploaded garment original. Created when the
/// original is uploaded (before the garment exists), then linked to the garment at "Add" via
/// <see cref="GarmentId"/>. A hosted worker runs rembg and, once done, produces the cutout and
/// updates the linked garment. Mirrors the try-on job pattern.
/// </summary>
public sealed record BackgroundRemovalJob(
    Guid Id,
    string UserId,
    string OriginalObjectKey,
    string OriginalUrl,
    BackgroundRemovalStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    /// <summary>Set when a garment adopts this upload before removal finished; null while still in the queue.</summary>
    public Guid? GarmentId { get; init; }

    /// <summary>Category of the linked garment, so the worker can apply clothing-only auto-straighten.</summary>
    public GarmentCategory? GarmentCategory { get; init; }

    public string? CutoutUrl { get; init; }

    public string? ThumbnailUrl { get; init; }

    public string? Error { get; init; }
}
