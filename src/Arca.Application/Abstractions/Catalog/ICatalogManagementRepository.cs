using Arca.Application.Catalog;
using Arca.Application.Common;

namespace Arca.Application.Abstractions.Catalog;

public interface ICatalogManagementRepository
{
    Task<PagedResult<CategoryDto>> ListCategoriesAsync(Guid tenantId, PageRequest pageRequest, CancellationToken cancellationToken = default);
    Task<bool> CategorySlugExistsAsync(Guid tenantId, string slug, Guid? exceptId = null, CancellationToken cancellationToken = default);
    Task<CategoryDto> CreateCategoryAsync(CreateCategoryData data, CancellationToken cancellationToken = default);
    Task<CategoryDto?> UpdateCategoryAsync(UpdateCategoryData data, CancellationToken cancellationToken = default);
    Task<bool> DisableCategoryAsync(Guid tenantId, Guid categoryId, CancellationToken cancellationToken = default);

    Task<PagedResult<ProductTypeDto>> ListProductTypesAsync(Guid tenantId, PageRequest pageRequest, CancellationToken cancellationToken = default);
    Task<bool> ProductTypeNameExistsAsync(Guid tenantId, string name, Guid? exceptId = null, CancellationToken cancellationToken = default);
    Task<ProductTypeDto> CreateProductTypeAsync(CreateProductTypeData data, CancellationToken cancellationToken = default);
    Task<ProductTypeDto?> UpdateProductTypeAsync(UpdateProductTypeData data, CancellationToken cancellationToken = default);
    Task<bool> DisableProductTypeAsync(Guid tenantId, Guid productTypeId, CancellationToken cancellationToken = default);

    Task<PagedResult<AttributeDto>> ListAttributesAsync(Guid tenantId, PageRequest pageRequest, CancellationToken cancellationToken = default);
    Task<bool> AttributeCodeExistsAsync(Guid tenantId, string code, Guid? exceptId = null, CancellationToken cancellationToken = default);
    Task<AttributeDto> CreateAttributeAsync(CreateAttributeData data, CancellationToken cancellationToken = default);
    Task<AttributeDto?> UpdateAttributeAsync(UpdateAttributeData data, CancellationToken cancellationToken = default);
    Task<bool> DisableAttributeAsync(Guid tenantId, Guid attributeId, CancellationToken cancellationToken = default);

    Task<PagedResult<AttributeValueDto>> ListAttributeValuesAsync(Guid tenantId, Guid attributeId, PageRequest pageRequest, CancellationToken cancellationToken = default);
    Task<bool> AttributeValueCodeExistsAsync(Guid tenantId, Guid attributeId, string code, Guid? exceptId = null, CancellationToken cancellationToken = default);
    Task<AttributeValueDto> CreateAttributeValueAsync(CreateAttributeValueData data, CancellationToken cancellationToken = default);
    Task<AttributeValueDto?> UpdateAttributeValueAsync(UpdateAttributeValueData data, CancellationToken cancellationToken = default);
    Task<bool> DisableAttributeValueAsync(Guid tenantId, Guid attributeId, Guid valueId, CancellationToken cancellationToken = default);

    Task<PagedResult<ProductSummaryDto>> ListProductsAsync(Guid tenantId, PageRequest pageRequest, CancellationToken cancellationToken = default);
    Task<ProductDetailsDto?> GetProductAsync(Guid tenantId, Guid productId, CancellationToken cancellationToken = default);
    Task<ProductSummaryDto?> UpdateProductAsync(UpdateProductData data, CancellationToken cancellationToken = default);
    Task<bool> DisableProductAsync(Guid tenantId, Guid productId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<ProductVariantDto>> ListVariantsAsync(Guid tenantId, Guid productId, CancellationToken cancellationToken = default);
}
