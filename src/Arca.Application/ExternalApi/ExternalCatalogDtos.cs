namespace Arca.Application.ExternalApi;

public sealed record ExternalCategoryDto(
    Guid Id,
    Guid? ParentCategoryId,
    string Name,
    string Slug,
    string? Description,
    int SortOrder);

public sealed record ExternalProductListItemDto(
    Guid Id,
    Guid? CategoryId,
    Guid? ProductTypeId,
    string Name,
    string Slug,
    string? Description,
    string BaseSku,
    string? Brand,
    string Status,
    string? MainImageUrl);

public sealed record ExternalProductDto(
    Guid Id,
    Guid? CategoryId,
    Guid? ProductTypeId,
    string Name,
    string Slug,
    string? Description,
    string BaseSku,
    string? Barcode,
    string? Brand,
    string Status,
    IReadOnlyCollection<ExternalProductImageDto> Images);

public sealed record ExternalProductVariantDto(
    Guid Id,
    Guid ProductId,
    string Sku,
    string? Barcode,
    string Name,
    decimal DefaultSalePrice,
    decimal? DefaultCostPrice,
    string Status,
    IReadOnlyCollection<ExternalVariantAttributeDto> Attributes);

public sealed record ExternalVariantAttributeDto(
    Guid ProductAttributeId,
    string AttributeName,
    Guid ProductAttributeValueId,
    string ValueName,
    string ValueCode);

public sealed record ExternalProductImageDto(
    Guid Id,
    Guid ProductId,
    Guid? ProductVariantId,
    string FileName,
    string ContentType,
    string? PublicUrl,
    string? AltText,
    int SortOrder,
    bool IsMain);

public sealed record ExternalInventoryAvailabilityDto(
    Guid ProductVariantId,
    Guid? StoreId,
    int AvailableQuantity);
