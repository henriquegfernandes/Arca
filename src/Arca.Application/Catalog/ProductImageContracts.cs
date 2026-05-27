namespace Arca.Application.Catalog;

public sealed record UploadProductImageCommand(
    Guid TenantId,
    Guid ProductId,
    Guid? ProductVariantId,
    string OriginalFileName,
    string ContentType,
    long Length,
    Stream Content,
    string? AltText,
    int SortOrder,
    bool IsMain,
    Guid? RequestedByUserId);

public sealed record ProductImageDto(
    Guid Id,
    Guid ProductId,
    Guid? ProductVariantId,
    string FileName,
    string OriginalFileName,
    string ContentType,
    string StorageProvider,
    string StoragePath,
    string? PublicUrl,
    string? AltText,
    int SortOrder,
    bool IsMain,
    DateTime CreatedAt);

public sealed record ProductImageUploadResult(ProductImageDto Image);

public sealed record AddProductImageData(
    Guid Id,
    Guid TenantId,
    Guid ProductId,
    Guid? ProductVariantId,
    string FileName,
    string OriginalFileName,
    string ContentType,
    string StorageProvider,
    string StoragePath,
    string PublicUrl,
    string? AltText,
    int SortOrder,
    bool IsMain,
    Guid? RequestedByUserId);
