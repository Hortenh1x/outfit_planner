using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using OutfitPlanner.Application.Abstractions;

namespace OutfitPlanner.Infrastructure.Storage;

public sealed record MinioObjectStorageSettings(
    string Endpoint,
    string AccessKey,
    string SecretKey,
    string BucketName,
    bool ForcePathStyle = true,
    string Region = "us-east-1");

public class MinioObjectStorage : IObjectStorage, IDisposable
{
    private readonly IAmazonS3 _client;
    private readonly string _bucketName;
    private readonly bool _ownsClient;

    public MinioObjectStorage(MinioObjectStorageSettings settings)
        : this(CreateClient(settings), settings.BucketName, ownsClient: true)
    {
    }

    public MinioObjectStorage(IAmazonS3 client, string bucketName)
        : this(client, bucketName, ownsClient: false)
    {
    }

    private MinioObjectStorage(IAmazonS3 client, string bucketName, bool ownsClient)
    {
        _client = client;
        _bucketName = string.IsNullOrWhiteSpace(bucketName)
            ? throw new InvalidOperationException("Object storage bucket is required.")
            : bucketName.Trim();
        _ownsClient = ownsClient;
    }

    public StoredObject PutObject(ObjectStoragePutRequest request)
    {
        var key = NormalizeObjectKey(request.ObjectKey);

        // S3 needs a known content length; buffer non-seekable streams so the recorded length is
        // accurate (rather than 0) and the upload does not depend on chunked-transfer support.
        Stream content = request.Content;
        MemoryStream? buffered = null;
        if (!content.CanSeek)
        {
            buffered = new MemoryStream();
            content.CopyTo(buffered);
            buffered.Position = 0;
            content = buffered;
        }

        try
        {
            var length = content.Length;
            _client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = key,
                InputStream = content,
                ContentType = request.ContentType
            }).GetAwaiter().GetResult();
            return new StoredObject(key, request.ContentType, length, request.Private);
        }
        finally
        {
            buffered?.Dispose();
        }
    }

    public StoredObjectFile? GetObject(string objectKey)
    {
        // S3/MinIO has no local file path; objects are served to clients via presigned URLs
        // (CreateSignedReadUrl) and read server-side via OpenReadObject.
        return null;
    }

    public Stream? OpenReadObject(string objectKey)
    {
        var key = NormalizeObjectKey(objectKey);
        try
        {
            using var response = _client.GetObjectAsync(_bucketName, key).GetAwaiter().GetResult();
            var buffer = new MemoryStream();
            response.ResponseStream.CopyTo(buffer);
            buffer.Position = 0;
            return buffer;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public bool DeleteObject(string objectKey)
    {
        var key = NormalizeObjectKey(objectKey);
        try
        {
            _client.GetObjectMetadataAsync(_bucketName, key).GetAwaiter().GetResult();
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }

        _client.DeleteObjectAsync(_bucketName, key).GetAwaiter().GetResult();
        return true;
    }

    public int DeletePrefix(string prefix)
    {
        var normalizedPrefix = NormalizeObjectKey(prefix).TrimEnd('/') + "/";
        var deleted = 0;
        string? continuation = null;
        do
        {
            var listed = _client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = _bucketName,
                Prefix = normalizedPrefix,
                ContinuationToken = continuation
            }).GetAwaiter().GetResult();

            if (listed.S3Objects.Count > 0)
            {
                _client.DeleteObjectsAsync(new DeleteObjectsRequest
                {
                    BucketName = _bucketName,
                    Objects = listed.S3Objects.Select(item => new KeyVersion { Key = item.Key }).ToList()
                }).GetAwaiter().GetResult();
                deleted += listed.S3Objects.Count;
            }

            continuation = listed.IsTruncated == true ? listed.NextContinuationToken : null;
        }
        while (continuation is not null);

        return deleted;
    }

    public string CreateSignedReadUrl(string objectKey, TimeSpan lifetime)
    {
        var key = NormalizeObjectKey(objectKey);
        return _client.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = key,
            Expires = DateTime.UtcNow.Add(lifetime),
            Verb = HttpVerb.GET
        });
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _client.Dispose();
        }
    }

    private static AmazonS3Client CreateClient(MinioObjectStorageSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Endpoint)
            || string.IsNullOrWhiteSpace(settings.AccessKey)
            || string.IsNullOrWhiteSpace(settings.SecretKey))
        {
            throw new InvalidOperationException("S3-compatible object storage requires endpoint, access key, and secret key.");
        }

        var config = new AmazonS3Config
        {
            ServiceURL = settings.Endpoint,
            ForcePathStyle = settings.ForcePathStyle,
            AuthenticationRegion = settings.Region,
            RegionEndpoint = RegionEndpoint.GetBySystemName(settings.Region)
        };

        return new AmazonS3Client(new BasicAWSCredentials(settings.AccessKey, settings.SecretKey), config);
    }

    private static string NormalizeObjectKey(string objectKey)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
        {
            throw new InvalidOperationException("Object key is required.");
        }

        var normalized = objectKey.Trim().Replace('\\', '/').TrimStart('/');
        if (normalized.Contains("..", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Object key is not safe.");
        }

        return normalized;
    }
}

public sealed class S3ObjectStorage : MinioObjectStorage
{
    public S3ObjectStorage(MinioObjectStorageSettings settings)
        : base(settings)
    {
    }
}
