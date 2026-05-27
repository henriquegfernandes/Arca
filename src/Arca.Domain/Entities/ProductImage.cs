using Arca.Domain.Enums;

namespace Arca.Domain.Entities;

public sealed class ProductImage : BaseEntity
{
    public Guid ProductId { get; private set; }
    public Guid? ProductVariantId { get; private set; }
    public string FileName { get; private set; }
    public string OriginalFileName { get; private set; }
    public string ContentType { get; private set; }
    public StorageProvider StorageProvider { get; private set; }
    public string StoragePath { get; private set; }
    public string? PublicUrl { get; private set; }
    public string? AltText { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsMain { get; private set; }

    public ProductImage(
        Guid productId,
        Guid? productVariantId,
        string fileName,
        string originalFileName,
        string contentType,
        StorageProvider storageProvider,
        string storagePath,
        int sortOrder,
        bool isMain)
    {
        ProductId = productId;
        ProductVariantId = productVariantId;
        FileName = RequireText(fileName, nameof(fileName));
        OriginalFileName = RequireText(originalFileName, nameof(originalFileName));
        ContentType = RequireText(contentType, nameof(contentType));
        StorageProvider = storageProvider;
        StoragePath = RequireText(storagePath, nameof(storagePath));
        SortOrder = sortOrder;
        IsMain = isMain;
    }

    private static string RequireText(string value, string name) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{name} is required.", name) : value.Trim();
}
