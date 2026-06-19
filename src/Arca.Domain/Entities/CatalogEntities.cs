using Arca.Domain.Enums;

namespace Arca.Domain.Entities;

public sealed class Category : BaseEntity
{
    public Guid TenantId { get; private set; }
    public Guid? ParentCategoryId { get; private set; }
    public string Name { get; private set; }
    public string Slug { get; private set; }
    public string? Description { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }

    public Category(Guid tenantId, string name, string slug, int sortOrder, Guid? parentCategoryId = null, string? description = null)
    {
        TenantId = tenantId;
        ParentCategoryId = parentCategoryId;
        Name = RequireText(name, nameof(name));
        Slug = RequireText(slug, nameof(slug)).ToLowerInvariant();
        Description = description;
        SortOrder = sortOrder;
        IsActive = true;
    }

    private static string RequireText(string value, string name) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{name} is required.", name) : value.Trim();
}

public sealed class ProductType : BaseEntity
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }

    public ProductType(Guid tenantId, string name, string? description = null)
    {
        TenantId = tenantId;
        Name = RequireText(name, nameof(name));
        Description = description;
        IsActive = true;
    }

    private static string RequireText(string value, string name) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{name} is required.", name) : value.Trim();
}

public sealed class ProductAttribute : BaseEntity
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; }
    public string Code { get; private set; }
    public string? Description { get; private set; }
    public AttributeType AttributeType { get; private set; }
    public bool IsVariantAttribute { get; private set; }
    public bool IsRequired { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }

    public ProductAttribute(Guid tenantId, string name, string code, AttributeType attributeType, bool isVariantAttribute, bool isRequired, int sortOrder)
    {
        TenantId = tenantId;
        Name = RequireText(name, nameof(name));
        Code = RequireText(code, nameof(code)).ToUpperInvariant();
        AttributeType = attributeType;
        IsVariantAttribute = isVariantAttribute;
        IsRequired = isRequired;
        SortOrder = sortOrder;
        IsActive = true;
    }

    private static string RequireText(string value, string name) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{name} is required.", name) : value.Trim();
}

public sealed class ProductAttributeValue : BaseEntity
{
    public Guid TenantId { get; private set; }
    public Guid ProductAttributeId { get; private set; }
    public string Name { get; private set; }
    public string Code { get; private set; }
    public string? Value { get; private set; }
    public string? HexCode { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }

    public ProductAttributeValue(Guid tenantId, Guid productAttributeId, string name, string code, int sortOrder, string? value = null, string? hexCode = null)
    {
        TenantId = tenantId;
        ProductAttributeId = productAttributeId;
        Name = RequireText(name, nameof(name));
        Code = RequireText(code, nameof(code)).ToUpperInvariant();
        Value = value;
        HexCode = hexCode;
        SortOrder = sortOrder;
        IsActive = true;
    }

    private static string RequireText(string value, string name) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{name} is required.", name) : value.Trim();
}

public sealed class Product : BaseEntity
{
    public Guid TenantId { get; private set; }
    public Guid? CategoryId { get; private set; }
    public Guid? ProductTypeId { get; private set; }
    public string Name { get; private set; }
    public string Slug { get; private set; }
    public string? Description { get; private set; }
    public string BaseSku { get; private set; }
    public string? Barcode { get; private set; }
    public string? Brand { get; private set; }
    public ProductStatus Status { get; private set; }

    public Product(Guid tenantId, string name, string slug, string baseSku, ProductStatus status = ProductStatus.Active)
    {
        TenantId = tenantId;
        Name = RequireText(name, nameof(name));
        Slug = RequireText(slug, nameof(slug)).ToLowerInvariant();
        BaseSku = RequireText(baseSku, nameof(baseSku)).ToUpperInvariant();
        Status = status;
    }

    private static string RequireText(string value, string name) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{name} is required.", name) : value.Trim();
}

public sealed class ProductVariant : BaseEntity
{
    public Guid ProductId { get; private set; }
    public string Sku { get; private set; }
    public string? Barcode { get; private set; }
    public string Name { get; private set; }
    public decimal DefaultSalePrice { get; private set; }
    public decimal? DefaultCostPrice { get; private set; }
    public ProductStatus Status { get; private set; }

    public ProductVariant(Guid productId, string sku, string name, decimal defaultSalePrice, decimal? defaultCostPrice, ProductStatus status)
    {
        if (defaultSalePrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(defaultSalePrice), "Sale price cannot be negative.");
        }

        ProductId = productId;
        Sku = RequireText(sku, nameof(sku)).ToUpperInvariant();
        Name = RequireText(name, nameof(name));
        DefaultSalePrice = defaultSalePrice;
        DefaultCostPrice = defaultCostPrice;
        Status = status;
    }

    private static string RequireText(string value, string name) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{name} is required.", name) : value.Trim();
}
