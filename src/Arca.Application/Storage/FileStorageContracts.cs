namespace Arca.Application.Storage;

public interface IFileStorageService
{
    Task<StoredFileResult> UploadAsync(FileUploadRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default);
    Task<Stream> GetAsync(string storagePath, CancellationToken cancellationToken = default);
}

public sealed record FileUploadRequest(
    Stream Content,
    string StoragePath,
    string FileName,
    string OriginalFileName,
    string ContentType);

public sealed record StoredFileResult(
    string StorageProvider,
    string StoragePath,
    string PublicUrl,
    string FileName,
    string ContentType);
