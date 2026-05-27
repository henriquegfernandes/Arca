using Arca.Application.Abstractions.Catalog;
using Arca.Application.Common;

namespace Arca.Application.Catalog;

public sealed class CatalogManagementService(ICatalogManagementRepository repository)
{
    public async Task<Result<PagedResult<CategoryDto>>> ListCategoriesAsync(
        Guid tenantId, PageRequest pageRequest, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return Result<PagedResult<CategoryDto>>.Failure("TenantId is required.");

        var categories = await repository.ListCategoriesAsync(tenantId, pageRequest, cancellationToken);
        return Result<PagedResult<CategoryDto>>.Success(categories);
    }

    public async Task<Result<CategoryDto>> CreateCategoryAsync(
        CreateCategoryCommand command, CancellationToken cancellationToken = default)
    {
        var validationError = await ValidateCategoryAsync(
            command.TenantId, null, command.Name, command.ParentCategoryId, cancellationToken);
        if (validationError is not null)
            return Result<CategoryDto>.Failure(validationError);

        var slug = NormalizeSlug(command.Name);
        if (await repository.CategorySlugExistsAsync(command.TenantId, slug, null, cancellationToken))
            return Result<CategoryDto>.Failure("A category with this name already exists.");

        var category = await repository.CreateCategoryAsync(new CreateCategoryData(
            command.TenantId,
            command.ParentCategoryId,
            command.Name.Trim(),
            slug,
            TrimToNull(command.Description),
            command.SortOrder,
            command.RequestedByUserId), cancellationToken);

        return Result<CategoryDto>.Success(category);
    }

    public async Task<Result<CategoryDto>> UpdateCategoryAsync(
        UpdateCategoryCommand command, CancellationToken cancellationToken = default)
    {
        if (command.CategoryId == Guid.Empty)
            return Result<CategoryDto>.Failure("CategoryId is required.");

        var validationError = await ValidateCategoryAsync(
            command.TenantId, command.CategoryId, command.Name, command.ParentCategoryId, cancellationToken);
        if (validationError is not null)
            return Result<CategoryDto>.Failure(validationError);

        var slug = NormalizeSlug(command.Name);
        if (await repository.CategorySlugExistsAsync(command.TenantId, slug, command.CategoryId, cancellationToken))
            return Result<CategoryDto>.Failure("A category with this name already exists.");

        var category = await repository.UpdateCategoryAsync(new UpdateCategoryData(
            command.TenantId, command.CategoryId, command.ParentCategoryId,
            command.Name.Trim(), slug, TrimToNull(command.Description),
            command.SortOrder, command.RequestedByUserId), cancellationToken);

        return category is null
            ? Result<CategoryDto>.Failure("Category was not found.")
            : Result<CategoryDto>.Success(category);
    }

    public async Task<Result> DisableCategoryAsync(
        Guid tenantId, Guid categoryId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || categoryId == Guid.Empty)
            return Result.Failure("TenantId and CategoryId are required.");

        var disabled = await repository.DisableCategoryAsync(tenantId, categoryId, cancellationToken);
        return disabled ? Result.Success() : Result.Failure("Category was not found.");
    }

    public async Task<Result<PagedResult<ProductTypeDto>>> ListProductTypesAsync(
        Guid tenantId, PageRequest pageRequest, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return Result<PagedResult<ProductTypeDto>>.Failure("TenantId is required.");

        var types = await repository.ListProductTypesAsync(tenantId, pageRequest, cancellationToken);
        return Result<PagedResult<ProductTypeDto>>.Success(types);
    }

    public async Task<Result<ProductTypeDto>> CreateProductTypeAsync(
        CreateProductTypeCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            return Result<ProductTypeDto>.Failure("Product type name is required.");

        if (await repository.ProductTypeNameExistsAsync(command.TenantId, command.Name.Trim(), null, cancellationToken))
            return Result<ProductTypeDto>.Failure("A product type with this name already exists.");

        var type = await repository.CreateProductTypeAsync(new CreateProductTypeData(
            command.TenantId, command.Name.Trim(), TrimToNull(command.Description),
            command.RequestedByUserId), cancellationToken);

        return Result<ProductTypeDto>.Success(type);
    }

    public async Task<Result<ProductTypeDto>> UpdateProductTypeAsync(
        UpdateProductTypeCommand command, CancellationToken cancellationToken = default)
    {
        if (command.ProductTypeId == Guid.Empty)
            return Result<ProductTypeDto>.Failure("ProductTypeId is required.");

        if (string.IsNullOrWhiteSpace(command.Name))
            return Result<ProductTypeDto>.Failure("Product type name is required.");

        if (await repository.ProductTypeNameExistsAsync(command.TenantId, command.Name.Trim(), command.ProductTypeId, cancellationToken))
            return Result<ProductTypeDto>.Failure("A product type with this name already exists.");

        var type = await repository.UpdateProductTypeAsync(new UpdateProductTypeData(
            command.TenantId, command.ProductTypeId, command.Name.Trim(),
            TrimToNull(command.Description), command.RequestedByUserId), cancellationToken);

        return type is null
            ? Result<ProductTypeDto>.Failure("Product type was not found.")
            : Result<ProductTypeDto>.Success(type);
    }

    public async Task<Result> DisableProductTypeAsync(
        Guid tenantId, Guid productTypeId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || productTypeId == Guid.Empty)
            return Result.Failure("TenantId and ProductTypeId are required.");

        var disabled = await repository.DisableProductTypeAsync(tenantId, productTypeId, cancellationToken);
        return disabled ? Result.Success() : Result.Failure("Product type was not found.");
    }

    public async Task<Result<PagedResult<AttributeDto>>> ListAttributesAsync(
        Guid tenantId, PageRequest pageRequest, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return Result<PagedResult<AttributeDto>>.Failure("TenantId is required.");

        var attributes = await repository.ListAttributesAsync(tenantId, pageRequest, cancellationToken);
        return Result<PagedResult<AttributeDto>>.Success(attributes);
    }

    public async Task<Result<AttributeDto>> CreateAttributeAsync(
        CreateAttributeCommand command, CancellationToken cancellationToken = default)
    {
        var validationError = ValidateAttribute(command);
        if (validationError is not null)
            return Result<AttributeDto>.Failure(validationError);

        var code = NormalizeAttributeCode(command.Code);
        if (await repository.AttributeCodeExistsAsync(command.TenantId, code, null, cancellationToken))
            return Result<AttributeDto>.Failure("An attribute with this code already exists.");

        var attribute = await repository.CreateAttributeAsync(new CreateAttributeData(
            command.TenantId, command.Name.Trim(), code, TrimToNull(command.Description),
            NormalizeAttributeType(command.AttributeType), command.IsVariantAttribute,
            command.IsRequired, command.SortOrder, command.RequestedByUserId), cancellationToken);

        return Result<AttributeDto>.Success(attribute);
    }

    public async Task<Result<AttributeDto>> UpdateAttributeAsync(
        UpdateAttributeCommand command, CancellationToken cancellationToken = default)
    {
        if (command.AttributeId == Guid.Empty)
            return Result<AttributeDto>.Failure("AttributeId is required.");

        var validationError = ValidateAttribute(new CreateAttributeCommand
        {
            Name = command.Name, Code = command.Code, AttributeType = command.AttributeType
        });
        if (validationError is not null)
            return Result<AttributeDto>.Failure(validationError);

        var code = NormalizeAttributeCode(command.Code);
        if (await repository.AttributeCodeExistsAsync(command.TenantId, code, command.AttributeId, cancellationToken))
            return Result<AttributeDto>.Failure("An attribute with this code already exists.");

        var attribute = await repository.UpdateAttributeAsync(new UpdateAttributeData(
            command.TenantId, command.AttributeId, command.Name.Trim(), code,
            TrimToNull(command.Description), NormalizeAttributeType(command.AttributeType),
            command.IsVariantAttribute, command.IsRequired, command.SortOrder,
            command.RequestedByUserId), cancellationToken);

        return attribute is null
            ? Result<AttributeDto>.Failure("Attribute was not found.")
            : Result<AttributeDto>.Success(attribute);
    }

    public async Task<Result> DisableAttributeAsync(
        Guid tenantId, Guid attributeId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || attributeId == Guid.Empty)
            return Result.Failure("TenantId and AttributeId are required.");

        var disabled = await repository.DisableAttributeAsync(tenantId, attributeId, cancellationToken);
        return disabled ? Result.Success() : Result.Failure("Attribute was not found.");
    }

    public async Task<Result<PagedResult<AttributeValueDto>>> ListAttributeValuesAsync(
        Guid tenantId, Guid attributeId, PageRequest pageRequest, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || attributeId == Guid.Empty)
            return Result<PagedResult<AttributeValueDto>>.Failure("TenantId and AttributeId are required.");

        var values = await repository.ListAttributeValuesAsync(tenantId, attributeId, pageRequest, cancellationToken);
        return Result<PagedResult<AttributeValueDto>>.Success(values);
    }

    public async Task<Result<AttributeValueDto>> CreateAttributeValueAsync(
        CreateAttributeValueCommand command, CancellationToken cancellationToken = default)
    {
        var validationError = ValidateAttributeValue(command);
        if (validationError is not null)
            return Result<AttributeValueDto>.Failure(validationError);

        var code = NormalizeAttributeCode(command.Code);
        if (await repository.AttributeValueCodeExistsAsync(command.TenantId, command.ProductAttributeId, code, null, cancellationToken))
            return Result<AttributeValueDto>.Failure("An attribute value with this code already exists.");

        var value = await repository.CreateAttributeValueAsync(new CreateAttributeValueData(
            command.TenantId, command.ProductAttributeId, command.Name.Trim(), code,
            TrimToNull(command.Value), TrimToNull(command.HexCode),
            command.SortOrder, command.RequestedByUserId), cancellationToken);

        return Result<AttributeValueDto>.Success(value);
    }

    public async Task<Result<AttributeValueDto>> UpdateAttributeValueAsync(
        UpdateAttributeValueCommand command, CancellationToken cancellationToken = default)
    {
        if (command.ValueId == Guid.Empty)
            return Result<AttributeValueDto>.Failure("ValueId is required.");

        var validationError = ValidateAttributeValue(new CreateAttributeValueCommand
        {
            Name = command.Name, Code = command.Code
        });
        if (validationError is not null)
            return Result<AttributeValueDto>.Failure(validationError);

        var code = NormalizeAttributeCode(command.Code);
        if (await repository.AttributeValueCodeExistsAsync(command.TenantId, command.ProductAttributeId, code, command.ValueId, cancellationToken))
            return Result<AttributeValueDto>.Failure("An attribute value with this code already exists.");

        var value = await repository.UpdateAttributeValueAsync(new UpdateAttributeValueData(
            command.TenantId, command.ProductAttributeId, command.ValueId, command.Name.Trim(), code,
            TrimToNull(command.Value), TrimToNull(command.HexCode),
            command.SortOrder, command.RequestedByUserId), cancellationToken);

        return value is null
            ? Result<AttributeValueDto>.Failure("Attribute value was not found.")
            : Result<AttributeValueDto>.Success(value);
    }

    public async Task<Result> DisableAttributeValueAsync(
        Guid tenantId, Guid attributeId, Guid valueId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || attributeId == Guid.Empty || valueId == Guid.Empty)
            return Result.Failure("TenantId, AttributeId and ValueId are required.");

        var disabled = await repository.DisableAttributeValueAsync(tenantId, attributeId, valueId, cancellationToken);
        return disabled ? Result.Success() : Result.Failure("Attribute value was not found.");
    }

    public async Task<Result<PagedResult<ProductSummaryDto>>> ListProductsAsync(
        Guid tenantId, PageRequest pageRequest, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return Result<PagedResult<ProductSummaryDto>>.Failure("TenantId is required.");

        var products = await repository.ListProductsAsync(tenantId, pageRequest, cancellationToken);
        return Result<PagedResult<ProductSummaryDto>>.Success(products);
    }

    public async Task<Result<ProductDetailsDto>> GetProductAsync(
        Guid tenantId, Guid productId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || productId == Guid.Empty)
            return Result<ProductDetailsDto>.Failure("TenantId and ProductId are required.");

        var product = await repository.GetProductAsync(tenantId, productId, cancellationToken);
        return product is null
            ? Result<ProductDetailsDto>.Failure("Product was not found.")
            : Result<ProductDetailsDto>.Success(product);
    }

    public async Task<Result<ProductSummaryDto>> UpdateProductAsync(
        UpdateProductCommand command, CancellationToken cancellationToken = default)
    {
        if (command.ProductId == Guid.Empty)
            return Result<ProductSummaryDto>.Failure("ProductId is required.");

        if (string.IsNullOrWhiteSpace(command.Name))
            return Result<ProductSummaryDto>.Failure("Product name is required.");

        if (string.IsNullOrWhiteSpace(command.BaseSku))
            return Result<ProductSummaryDto>.Failure("BaseSku is required.");

        if (string.IsNullOrWhiteSpace(command.Slug))
            return Result<ProductSummaryDto>.Failure("Slug is required.");

        var normalizedStatus = NormalizeStatus(command.Status);
        var product = await repository.UpdateProductAsync(new UpdateProductData(
            command.TenantId, command.ProductId, command.CategoryId, command.ProductTypeId,
            command.Name.Trim(), NormalizeSlug(command.Slug), TrimToNull(command.Description),
            command.BaseSku.Trim().ToUpperInvariant(), TrimToNull(command.Barcode),
            TrimToNull(command.Brand), normalizedStatus, command.RequestedByUserId), cancellationToken);

        return product is null
            ? Result<ProductSummaryDto>.Failure("Product was not found.")
            : Result<ProductSummaryDto>.Success(product);
    }

    public async Task<Result> DisableProductAsync(
        Guid tenantId, Guid productId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || productId == Guid.Empty)
            return Result.Failure("TenantId and ProductId are required.");

        var disabled = await repository.DisableProductAsync(tenantId, productId, cancellationToken);
        return disabled ? Result.Success() : Result.Failure("Product was not found.");
    }

    public async Task<Result<IReadOnlyCollection<ProductVariantDto>>> ListVariantsAsync(
        Guid tenantId, Guid productId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || productId == Guid.Empty)
            return Result<IReadOnlyCollection<ProductVariantDto>>.Failure("TenantId and ProductId are required.");

        var variants = await repository.ListVariantsAsync(tenantId, productId, cancellationToken);
        return Result<IReadOnlyCollection<ProductVariantDto>>.Success(variants);
    }

    private async Task<string?> ValidateCategoryAsync(
        Guid tenantId, Guid? categoryId, string name, Guid? parentCategoryId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Category name is required.";

        if (categoryId.HasValue && parentCategoryId == categoryId)
            return "A category cannot be its own parent.";

        return null;
    }

    private static string? ValidateAttribute(CreateAttributeCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            return "Attribute name is required.";

        if (string.IsNullOrWhiteSpace(command.Code))
            return "Attribute code is required.";

        var validTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Select", "MultiSelect", "Text", "Number", "Boolean", "Date", "Decimal"
        };

        return validTypes.Contains(command.AttributeType) ? null
            : "Attribute type must be Select, MultiSelect, Text, Number, Boolean, Date or Decimal.";
    }

    private static string? ValidateAttributeValue(CreateAttributeValueCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            return "Attribute value name is required.";

        if (string.IsNullOrWhiteSpace(command.Code))
            return "Attribute value code is required.";

        return null;
    }

    private static string NormalizeSlug(string value) =>
        value.Trim().ToLowerInvariant().Replace(" ", "-", StringComparison.Ordinal)
            .Replace("--", "-", StringComparison.Ordinal);

    private static string NormalizeAttributeCode(string value) =>
        value.Trim().ToUpperInvariant().Replace(" ", "-", StringComparison.Ordinal);

    private static string NormalizeAttributeType(string type) =>
        type.Trim() switch
        {
            "select" => "Select",
            "multiselect" => "MultiSelect",
            "text" => "Text",
            "number" => "Number",
            "boolean" => "Boolean",
            "date" => "Date",
            "decimal" => "Decimal",
            _ => type.Trim()
        };

    private static string NormalizeStatus(string status) =>
        status.Trim().ToLowerInvariant() switch
        {
            "draft" => "Draft",
            "inactive" => "Inactive",
            _ => "Active"
        };

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
