using Arca.Application.Abstractions.Catalog;
using Arca.Application.Common;
using Arca.Application.Storage;

namespace Arca.Application.Catalog;

public sealed class ProductImageService(
    IProductImageRepository repository,
    IFileStorageService fileStorageService)
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/gif"
    };

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp",
        ".gif"
    };

    private const long MaxFileSize = 5 * 1024 * 1024;

    public async Task<Result<ProductImageUploadResult>> UploadAsync(
        UploadProductImageCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationError = await ValidateAsync(command, cancellationToken);
        if (validationError is not null)
        {
            return Result<ProductImageUploadResult>.Failure(validationError);
        }

        var imageId = Guid.NewGuid();
        var extension = NormalizeExtension(Path.GetExtension(command.OriginalFileName));
        var fileName = $"{imageId:N}{extension}";
        var storagePath = $"tenants/{command.TenantId:D}/products/{command.ProductId:D}/{fileName}";

        var stored = await fileStorageService.UploadAsync(
            new FileUploadRequest(
                command.Content,
                storagePath,
                fileName,
                command.OriginalFileName,
                command.ContentType),
            cancellationToken);

        try
        {
            var image = await repository.AddAsync(
                new AddProductImageData(
                    imageId,
                    command.TenantId,
                    command.ProductId,
                    command.ProductVariantId,
                    stored.FileName,
                    command.OriginalFileName,
                    stored.ContentType,
                    stored.StorageProvider,
                    stored.StoragePath,
                    stored.PublicUrl,
                    TrimToNull(command.AltText),
                    command.SortOrder,
                    command.IsMain,
                    command.RequestedByUserId),
                cancellationToken);

            return Result<ProductImageUploadResult>.Success(new ProductImageUploadResult(image));
        }
        catch
        {
            await fileStorageService.DeleteAsync(stored.StoragePath, cancellationToken);
            throw;
        }
    }

    public async Task<Result<IReadOnlyCollection<ProductImageDto>>> ListAsync(
        Guid tenantId,
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        if (!await repository.ProductBelongsToTenantAsync(tenantId, productId, cancellationToken))
        {
            return Result<IReadOnlyCollection<ProductImageDto>>.Failure("Product was not found for this tenant.");
        }

        var images = await repository.ListAsync(tenantId, productId, cancellationToken);
        return Result<IReadOnlyCollection<ProductImageDto>>.Success(images);
    }

    public async Task<Result> DeleteAsync(
        Guid tenantId,
        Guid productId,
        Guid imageId,
        Guid? requestedByUserId,
        CancellationToken cancellationToken = default)
    {
        var image = await repository.FindAsync(tenantId, productId, imageId, cancellationToken);
        if (image is null)
        {
            return Result.Failure("Image was not found for this tenant/product.");
        }

        await repository.DeleteAsync(tenantId, productId, imageId, requestedByUserId, cancellationToken);
        await fileStorageService.DeleteAsync(image.StoragePath, cancellationToken);

        return Result.Success();
    }

    private async Task<string?> ValidateAsync(
        UploadProductImageCommand command,
        CancellationToken cancellationToken)
    {
        if (command.TenantId == Guid.Empty)
        {
            return "TenantId is required.";
        }

        if (command.ProductId == Guid.Empty)
        {
            return "ProductId is required.";
        }

        if (command.ProductVariantId == Guid.Empty)
        {
            return "ProductVariantId must be null or a valid id.";
        }

        if (command.Content.CanRead is false || command.Length <= 0)
        {
            return "Image file is required.";
        }

        if (command.Length > MaxFileSize)
        {
            return "Image file cannot exceed 5 MiB.";
        }

        if (!AllowedContentTypes.Contains(command.ContentType))
        {
            return "Image content type is not supported.";
        }

        var extension = NormalizeExtension(Path.GetExtension(command.OriginalFileName));
        if (!AllowedExtensions.Contains(extension))
        {
            return "Image file extension is not supported.";
        }

        if (!await repository.ProductBelongsToTenantAsync(command.TenantId, command.ProductId, cancellationToken))
        {
            return "Product was not found for this tenant.";
        }

        if (command.ProductVariantId is not null
            && !await repository.VariantBelongsToProductAsync(command.ProductId, command.ProductVariantId.Value, cancellationToken))
        {
            return "Product variant was not found for this product.";
        }

        return null;
    }

    private static string NormalizeExtension(string extension) => extension.Trim().ToLowerInvariant();

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
