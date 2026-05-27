using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Arca.Application.Storage;
using Microsoft.Extensions.Configuration;

namespace Arca.Infrastructure.Storage;

public sealed class S3FileStorageService : IFileStorageService
{
    private readonly string _bucketName;
    private readonly string _publicBaseUrl;
    private readonly AmazonS3Client _client;

    public S3FileStorageService(IConfiguration configuration)
    {
        _bucketName = configuration["Storage:S3:BucketName"]
            ?? throw new InvalidOperationException("Storage:S3:BucketName must be configured for S3 storage.");

        _publicBaseUrl = (configuration["Storage:S3:PublicBaseUrl"] ?? string.Empty).TrimEnd('/');
        var region = configuration["Storage:S3:Region"] ?? "us-east-1";
        var accessKey = configuration["Storage:S3:AccessKey"]
            ?? throw new InvalidOperationException("Storage:S3:AccessKey must be configured for S3 storage.");
        var secretKey = configuration["Storage:S3:SecretKey"]
            ?? throw new InvalidOperationException("Storage:S3:SecretKey must be configured for S3 storage.");

        var config = new AmazonS3Config
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(region),
            ForcePathStyle = bool.TryParse(configuration["Storage:S3:ForcePathStyle"], out var forcePathStyle) && forcePathStyle
        };

        var serviceUrl = configuration["Storage:S3:ServiceUrl"];
        if (!string.IsNullOrWhiteSpace(serviceUrl))
        {
            config.ServiceURL = serviceUrl;
            config.RegionEndpoint = null;
        }

        _client = new AmazonS3Client(accessKey, secretKey, config);
    }

    public async Task<StoredFileResult> UploadAsync(
        FileUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        var storagePath = NormalizeStoragePath(request.StoragePath);

        await _client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = storagePath,
            InputStream = request.Content,
            ContentType = request.ContentType
        }, cancellationToken);

        return new StoredFileResult(
            "S3",
            storagePath,
            BuildPublicUrl(storagePath),
            request.FileName,
            request.ContentType);
    }

    public Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default) =>
        _client.DeleteObjectAsync(_bucketName, NormalizeStoragePath(storagePath), cancellationToken);

    public async Task<Stream> GetAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        using var response = await _client.GetObjectAsync(_bucketName, NormalizeStoragePath(storagePath), cancellationToken);
        var memoryStream = new MemoryStream();
        await response.ResponseStream.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0;

        return memoryStream;
    }

    private string BuildPublicUrl(string storagePath) =>
        string.IsNullOrWhiteSpace(_publicBaseUrl)
            ? storagePath
            : $"{_publicBaseUrl}/{storagePath}";

    private static string NormalizeStoragePath(string storagePath)
    {
        var normalized = storagePath.Replace('\\', '/').Trim('/');
        if (normalized.Contains("..", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Storage path cannot contain traversal segments.");
        }

        return normalized;
    }
}
