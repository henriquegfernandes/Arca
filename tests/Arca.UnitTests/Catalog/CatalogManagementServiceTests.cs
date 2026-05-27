using Arca.Application.Abstractions.Catalog;
using Arca.Application.Catalog;
using Arca.Application.Common;

namespace Arca.UnitTests.Catalog;

public sealed class CatalogManagementServiceTests
{
    [Fact]
    public async Task CreateCategoryAsync_NormalizesSlugAndPersistsTrimmedValues()
    {
        var repository = new InMemoryCatalogManagementRepository();
        var service = new CatalogManagementService(repository);
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var result = await service.CreateCategoryAsync(new CreateCategoryCommand
        {
            TenantId = tenantId,
            Name = "  Summer Pieces  ",
            Description = "  Seasonal catalog  ",
            SortOrder = 3,
            RequestedByUserId = userId
        });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Summer Pieces", result.Value!.Name);
        Assert.Equal("summer-pieces", result.Value.Slug);
        Assert.Equal("Seasonal catalog", result.Value.Description);
        Assert.Equal(userId, repository.LastCreateCategoryData?.RequestedByUserId);
    }

    [Fact]
    public async Task CreateCategoryAsync_DoesNotPersistWhenSlugAlreadyExists()
    {
        var repository = new InMemoryCatalogManagementRepository { ExistingCategorySlug = true };
        var service = new CatalogManagementService(repository);

        var result = await service.CreateCategoryAsync(new CreateCategoryCommand
        {
            TenantId = Guid.NewGuid(),
            Name = "Shoes"
        });

        Assert.True(result.IsFailure);
        Assert.Equal("A category with this name already exists.", result.Error);
        Assert.Null(repository.LastCreateCategoryData);
    }

    [Fact]
    public async Task CreateAttributeAsync_RejectsUnsupportedAttributeType()
    {
        var repository = new InMemoryCatalogManagementRepository();
        var service = new CatalogManagementService(repository);

        var result = await service.CreateAttributeAsync(new CreateAttributeCommand
        {
            TenantId = Guid.NewGuid(),
            Name = "Material",
            Code = "MAT",
            AttributeType = "Unsupported"
        });

        Assert.True(result.IsFailure);
        Assert.Equal("Attribute type must be Select, MultiSelect, Text, Number, Boolean, Date or Decimal.", result.Error);
    }

    private sealed class InMemoryCatalogManagementRepository : ICatalogManagementRepository
    {
        public bool ExistingCategorySlug { get; init; }
        public CreateCategoryData? LastCreateCategoryData { get; private set; }

        public Task<PagedResult<CategoryDto>> ListCategoriesAsync(Guid tenantId, PageRequest pageRequest, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PagedResult<CategoryDto>([], 0, pageRequest.NormalizedPage, pageRequest.NormalizedPageSize));

        public Task<bool> CategorySlugExistsAsync(Guid tenantId, string slug, Guid? exceptId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(ExistingCategorySlug);

        public Task<CategoryDto> CreateCategoryAsync(CreateCategoryData data, CancellationToken cancellationToken = default)
        {
            LastCreateCategoryData = data;
            return Task.FromResult(new CategoryDto(
                Guid.NewGuid(), data.TenantId, data.ParentCategoryId, data.Name, data.Slug,
                data.Description, data.SortOrder, true, DateTime.UtcNow));
        }

        public Task<CategoryDto?> UpdateCategoryAsync(UpdateCategoryData data, CancellationToken cancellationToken = default) =>
            Task.FromResult<CategoryDto?>(null);

        public Task<bool> DisableCategoryAsync(Guid tenantId, Guid categoryId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<PagedResult<ProductTypeDto>> ListProductTypesAsync(Guid tenantId, PageRequest pageRequest, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PagedResult<ProductTypeDto>([], 0, pageRequest.NormalizedPage, pageRequest.NormalizedPageSize));

        public Task<bool> ProductTypeNameExistsAsync(Guid tenantId, string name, Guid? exceptId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<ProductTypeDto> CreateProductTypeAsync(CreateProductTypeData data, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ProductTypeDto(Guid.NewGuid(), data.TenantId, data.Name, data.Description, true, DateTime.UtcNow));

        public Task<ProductTypeDto?> UpdateProductTypeAsync(UpdateProductTypeData data, CancellationToken cancellationToken = default) =>
            Task.FromResult<ProductTypeDto?>(null);

        public Task<bool> DisableProductTypeAsync(Guid tenantId, Guid productTypeId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<PagedResult<AttributeDto>> ListAttributesAsync(Guid tenantId, PageRequest pageRequest, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PagedResult<AttributeDto>([], 0, pageRequest.NormalizedPage, pageRequest.NormalizedPageSize));

        public Task<bool> AttributeCodeExistsAsync(Guid tenantId, string code, Guid? exceptId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<AttributeDto> CreateAttributeAsync(CreateAttributeData data, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AttributeDto(Guid.NewGuid(), data.TenantId, data.Name, data.Code, data.Description, data.AttributeType, data.IsVariantAttribute, data.IsRequired, data.SortOrder, true, DateTime.UtcNow));

        public Task<AttributeDto?> UpdateAttributeAsync(UpdateAttributeData data, CancellationToken cancellationToken = default) =>
            Task.FromResult<AttributeDto?>(null);

        public Task<bool> DisableAttributeAsync(Guid tenantId, Guid attributeId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<PagedResult<AttributeValueDto>> ListAttributeValuesAsync(Guid tenantId, Guid attributeId, PageRequest pageRequest, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PagedResult<AttributeValueDto>([], 0, pageRequest.NormalizedPage, pageRequest.NormalizedPageSize));

        public Task<bool> AttributeValueCodeExistsAsync(Guid tenantId, Guid attributeId, string code, Guid? exceptId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<AttributeValueDto> CreateAttributeValueAsync(CreateAttributeValueData data, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AttributeValueDto(Guid.NewGuid(), data.TenantId, data.ProductAttributeId, data.Name, data.Code, data.Value, data.HexCode, data.SortOrder, true, DateTime.UtcNow));

        public Task<AttributeValueDto?> UpdateAttributeValueAsync(UpdateAttributeValueData data, CancellationToken cancellationToken = default) =>
            Task.FromResult<AttributeValueDto?>(null);

        public Task<bool> DisableAttributeValueAsync(Guid tenantId, Guid attributeId, Guid valueId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<PagedResult<ProductSummaryDto>> ListProductsAsync(Guid tenantId, PageRequest pageRequest, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PagedResult<ProductSummaryDto>([], 0, pageRequest.NormalizedPage, pageRequest.NormalizedPageSize));

        public Task<ProductDetailsDto?> GetProductAsync(Guid tenantId, Guid productId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ProductDetailsDto?>(null);

        public Task<ProductSummaryDto?> UpdateProductAsync(UpdateProductData data, CancellationToken cancellationToken = default) =>
            Task.FromResult<ProductSummaryDto?>(null);

        public Task<bool> DisableProductAsync(Guid tenantId, Guid productId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<IReadOnlyCollection<ProductVariantDto>> ListVariantsAsync(Guid tenantId, Guid productId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyCollection<ProductVariantDto>>([]);
    }
}
