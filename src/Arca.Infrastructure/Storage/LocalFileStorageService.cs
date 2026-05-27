using Arca.Application.Storage;
using Microsoft.Extensions.Configuration;

namespace Arca.Infrastructure.Storage;

public sealed class LocalFileStorageService(IConfiguration configuration) : IFileStorageService
{
    private readonly string _basePath = EnsureTrailingSeparator(Path.GetFullPath(
        configuration["Storage:Local:BasePath"] ?? "wwwroot/uploads",
        Directory.GetCurrentDirectory()));

    private readonly string _publicBaseUrl = (configuration["Storage:Local:PublicBaseUrl"] ?? "/uploads").TrimEnd('/');

    public async Task<StoredFileResult> UploadAsync(
        FileUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        var storagePath = NormalizeStoragePath(request.StoragePath);
        var targetPath = ResolvePath(storagePath);

        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

        await using var fileStream = File.Create(targetPath);
        await request.Content.CopyToAsync(fileStream, cancellationToken);

        return new StoredFileResult(
            "Local",
            storagePath,
            $"{_publicBaseUrl}/{storagePath}",
            request.FileName,
            request.ContentType);
    }

    public Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(NormalizeStoragePath(storagePath));
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    public Task<Stream> GetAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(NormalizeStoragePath(storagePath));
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Stored file was not found.", storagePath);
        }

        return Task.FromResult<Stream>(File.OpenRead(path));
    }

    private string ResolvePath(string storagePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_basePath, storagePath));
        if (!fullPath.StartsWith(_basePath, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Storage path is outside the configured base path.");
        }

        return fullPath;
    }

    private static string EnsureTrailingSeparator(string path) =>
        path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

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
