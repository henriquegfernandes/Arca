namespace Arca.Application.Catalog;

public sealed record CategoryDto(
    Guid Id,
    Guid TenantId,
    Guid? ParentCategoryId,
    string Name,
    string Slug,
    string? Description,
    int SortOrder,
    bool IsActive,
    DateTime CreatedAt);

public sealed record ProductTypeDto(
    Guid Id,
    Guid TenantId,
    string Name,
    string? Description,
    bool IsActive,
    DateTime CreatedAt);

public sealed record AttributeDto(
    Guid Id,
    Guid TenantId,
    string Name,
    string Code,
    string? Description,
    string AttributeType,
    bool IsVariantAttribute,
    bool IsRequired,
    int SortOrder,
    bool IsActive,
    DateTime CreatedAt);

public sealed record AttributeValueDto(
    Guid Id,
    Guid TenantId,
    Guid ProductAttributeId,
    string Name,
    string Code,
    string? Value,
    string? HexCode,
    int SortOrder,
    bool IsActive,
    DateTime CreatedAt);

public sealed record ProductSummaryDto(
    Guid Id,
    Guid TenantId,
    Guid? CategoryId,
    Guid? ProductTypeId,
    string Name,
    string Slug,
    string? Description,
    string BaseSku,
    string? Barcode,
    string? Brand,
    string Status,
    int VariantCount,
    DateTime CreatedAt);

public sealed record ProductDetailsDto(
    Guid Id,
    Guid TenantId,
    Guid? CategoryId,
    string? CategoryName,
    Guid? ProductTypeId,
    string? ProductTypeName,
    string Name,
    string Slug,
    string? Description,
    string BaseSku,
    string? Barcode,
    string? Brand,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record ProductVariantDto(
    Guid Id,
    Guid ProductId,
    string Sku,
    string? Barcode,
    string Name,
    decimal DefaultSalePrice,
    decimal? DefaultCostPrice,
    string Status,
    DateTime CreatedAt);

public sealed class CreateCategoryCommand
{
    public Guid TenantId { get; init; }
    public Guid? ParentCategoryId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int SortOrder { get; init; }
    public Guid? RequestedByUserId { get; init; }
}

public sealed class UpdateCategoryCommand
{
    public Guid TenantId { get; init; }
    public Guid CategoryId { get; init; }
    public Guid? ParentCategoryId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int SortOrder { get; init; }
    public Guid? RequestedByUserId { get; init; }
}

public sealed class CreateProductTypeCommand
{
    public Guid TenantId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public Guid? RequestedByUserId { get; init; }
}

public sealed class UpdateProductTypeCommand
{
    public Guid TenantId { get; init; }
    public Guid ProductTypeId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public Guid? RequestedByUserId { get; init; }
}

public sealed class CreateAttributeCommand
{
    public Guid TenantId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string AttributeType { get; init; } = "Text";
    public bool IsVariantAttribute { get; init; }
    public bool IsRequired { get; init; }
    public int SortOrder { get; init; }
    public Guid? RequestedByUserId { get; init; }
}

public sealed class UpdateAttributeCommand
{
    public Guid TenantId { get; init; }
    public Guid AttributeId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string AttributeType { get; init; } = "Text";
    public bool IsVariantAttribute { get; init; }
    public bool IsRequired { get; init; }
    public int SortOrder { get; init; }
    public Guid? RequestedByUserId { get; init; }
}

public sealed class CreateAttributeValueCommand
{
    public Guid TenantId { get; init; }
    public Guid ProductAttributeId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string? Value { get; init; }
    public string? HexCode { get; init; }
    public int SortOrder { get; init; }
    public Guid? RequestedByUserId { get; init; }
}

public sealed class UpdateAttributeValueCommand
{
    public Guid TenantId { get; init; }
    public Guid ProductAttributeId { get; init; }
    public Guid ValueId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string? Value { get; init; }
    public string? HexCode { get; init; }
    public int SortOrder { get; init; }
    public Guid? RequestedByUserId { get; init; }
}

public sealed class UpdateProductCommand
{
    public Guid TenantId { get; init; }
    public Guid ProductId { get; init; }
    public Guid? CategoryId { get; init; }
    public Guid? ProductTypeId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string BaseSku { get; init; } = string.Empty;
    public string? Barcode { get; init; }
    public string? Brand { get; init; }
    public string Status { get; init; } = "Active";
    public Guid? RequestedByUserId { get; init; }
}

public sealed record CreateCategoryData(
    Guid TenantId,
    Guid? ParentCategoryId,
    string Name,
    string Slug,
    string? Description,
    int SortOrder,
    Guid? RequestedByUserId);

public sealed record UpdateCategoryData(
    Guid TenantId,
    Guid CategoryId,
    Guid? ParentCategoryId,
    string Name,
    string Slug,
    string? Description,
    int SortOrder,
    Guid? RequestedByUserId);

public sealed record CreateProductTypeData(
    Guid TenantId,
    string Name,
    string? Description,
    Guid? RequestedByUserId);

public sealed record UpdateProductTypeData(
    Guid TenantId,
    Guid ProductTypeId,
    string Name,
    string? Description,
    Guid? RequestedByUserId);

public sealed record CreateAttributeData(
    Guid TenantId,
    string Name,
    string Code,
    string? Description,
    string AttributeType,
    bool IsVariantAttribute,
    bool IsRequired,
    int SortOrder,
    Guid? RequestedByUserId);

public sealed record UpdateAttributeData(
    Guid TenantId,
    Guid AttributeId,
    string Name,
    string Code,
    string? Description,
    string AttributeType,
    bool IsVariantAttribute,
    bool IsRequired,
    int SortOrder,
    Guid? RequestedByUserId);

public sealed record CreateAttributeValueData(
    Guid TenantId,
    Guid ProductAttributeId,
    string Name,
    string Code,
    string? Value,
    string? HexCode,
    int SortOrder,
    Guid? RequestedByUserId);

public sealed record UpdateAttributeValueData(
    Guid TenantId,
    Guid ProductAttributeId,
    Guid ValueId,
    string Name,
    string Code,
    string? Value,
    string? HexCode,
    int SortOrder,
    Guid? RequestedByUserId);

public sealed record UpdateProductData(
    Guid TenantId,
    Guid ProductId,
    Guid? CategoryId,
    Guid? ProductTypeId,
    string Name,
    string Slug,
    string? Description,
    string BaseSku,
    string? Barcode,
    string? Brand,
    string Status,
    Guid? RequestedByUserId);
