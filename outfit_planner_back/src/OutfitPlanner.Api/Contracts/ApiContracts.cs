using OutfitPlanner.Domain;

namespace OutfitPlanner.Api.Contracts;

public sealed record CreateBodyReferencePhotoRequest(string ImageUrl);

public sealed record CreateGarmentRequest(
    string Name,
    GarmentCategory Category,
    string ImageUrl,
    string? ThumbnailUrl,
    IReadOnlyList<string>? Tags);

public sealed record CreateOutfitRequest(string Name, IReadOnlyList<Guid> GarmentIds);

public sealed record ScheduleOutfitRequest(DateOnly Date, Guid OutfitId);

public sealed record StartTryOnRequest(string BodyReferencePhotoUrl, bool ConsentAccepted, bool SequentialFlowEnabled);

public sealed record ShareLinkResponse(string Token, string Url);

public sealed record UploadedPhotoResponse(string FileName, string ContentType, long Length, string Url);

public sealed record RegisterRequest(string Email, string Password, string RepeatPassword);

public sealed record LoginRequest(string Email, string Password);

public sealed record AuthUserResponse(string Id, string? Email, string DisplayName);

public sealed record AuthSessionResponse(AuthUserResponse User, DateTimeOffset ExpiresAt);

public sealed record AuthProviderResponse(string Id, string Label, bool Configured, string Flow);
