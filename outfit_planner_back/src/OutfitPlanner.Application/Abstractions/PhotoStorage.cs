namespace OutfitPlanner.Application.Abstractions;

public sealed record IncomingPhoto(string FileName, string ContentType, long Length, Stream Content);

public sealed record StoredPhoto(string FileName, string ContentType, long Length, string Url);

public sealed record StoredPhotoFile(string FullPath, string ContentType);

public interface IPhotoStorage
{
    StoredPhoto SaveGarmentPhoto(IncomingPhoto photo);

    StoredPhoto SaveBodyReferencePhoto(IncomingPhoto photo);
}

public interface IStoredPhotoReader
{
    StoredPhotoFile? GetGarmentPhoto(string fileName);

    StoredPhotoFile? GetBodyReferencePhoto(string fileName);
}

public interface IStoredPhotoDeletion
{
    bool DeleteGarmentPhoto(string photoUrl);

    bool DeleteBodyReferencePhoto(string photoUrl);
}
