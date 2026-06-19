using Arca.Application.Abstractions.Catalog;
using Arca.Application.Abstractions.Inventory;
using Arca.Application.Catalog;
using Arca.Application.Common;
using Arca.Application.Export;
using Arca.Application.Inventory;

namespace Arca.UnitTests.Export;

public sealed class CsvExportServiceTests
{
    [Fact]
    public async Task ExportProductsAsync_ReturnsCsvWithHeadersAndRows()
    {
        var tenantId = Guid.NewGuid();
        var service = new CsvExportService(
            new InMemoryCatalogRepo(tenantId),
            new InMemoryInventoryRepo());

        var result = await service.ExportProductsAsync(tenantId);

        Assert.True(result.IsSuccess);
        var csv = System.Text.Encoding.UTF8.GetString(result.Value!);
        var lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(4, lines.Length); // header + 3 products
        Assert.StartsWith("Name,Slug,BaseSku", lines[0]);
        Assert.Contains("Product A", lines[1]);
    }

    [Fact]
    public async Task ExportProductsAsync_RejectsEmptyTenantId()
    {
        var service = new CsvExportService(
            new InMemoryCatalogRepo(Guid.NewGuid()),
            new InMemoryInventoryRepo());

        var result = await service.ExportProductsAsync(Guid.Empty);
        Assert.True(result.IsFailure);
        Assert.Equal("TenantId is required.", result.Error);
    }

    [Fact]
    public async Task ExportInventoryAsync_ReturnsCsvWithBalances()
    {
        var tenantId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var service = new CsvExportService(
            new InMemoryCatalogRepo(tenantId),
            new InMemoryInventoryRepo(storeId));

        var result = await service.ExportInventoryAsync(tenantId, storeId);

        Assert.True(result.IsSuccess);
        var csv = System.Text.Encoding.UTF8.GetString(result.Value!);
        var lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length); // header + 2 balances
        Assert.StartsWith("StockLocation,VariantSku", lines[0]);
    }

    [Fact]
    public async Task ExportMovementsAsync_ReturnsCsvWithMovements()
    {
        var tenantId = Guid.NewGuid();
        var storeId = Guid.NewGuid();
        var service = new CsvExportService(
            new InMemoryCatalogRepo(tenantId),
            new InMemoryInventoryRepo(storeId));

        var result = await service.ExportMovementsAsync(tenantId, storeId);

        Assert.True(result.IsSuccess);
        var csv = System.Text.Encoding.UTF8.GetString(result.Value!);
        var lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length); // header + 1 movement
        Assert.StartsWith("Type,Quantity", lines[0]);
    }

    private sealed class InMemoryCatalogRepo(Guid tenantId) : ICatalogManagementRepository
    {
        public Task<PagedResult<ProductSummaryDto>> ListProductsAsync(
            Guid requestTenantId, PageRequest pageRequest, CancellationToken cancellationToken = default)
        {
            var items = requestTenantId == tenantId
                ? new[]
                {
                    new ProductSummaryDto(Guid.NewGuid(), tenantId, null, null, "Product A", "product-a", null, "SKU-A", null, null, "Active", null, 0, DateTime.UtcNow),
                    new ProductSummaryDto(Guid.NewGuid(), tenantId, null, null, "Product B", "product-b", "Desc", "SKU-B", "BAR-B", "Brand", "Draft", null, 1, DateTime.UtcNow),
                    new ProductSummaryDto(Guid.NewGuid(), tenantId, null, null, "Product C", "product-c", null, "SKU-C", null, null, "Active", null, 0, DateTime.UtcNow)
                }
                : Array.Empty<ProductSummaryDto>();

            return Task.FromResult(new PagedResult<ProductSummaryDto>(items, items.Length, pageRequest.NormalizedPage, pageRequest.NormalizedPageSize));
        }

        public Task<PagedResult<CategoryDto>> ListCategoriesAsync(Guid tenantId, PageRequest pageRequest, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PagedResult<CategoryDto>([], 0, pageRequest.NormalizedPage, pageRequest.NormalizedPageSize));
        public Task<bool> CategorySlugExistsAsync(Guid tenantId, string slug, Guid? exceptId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> CategoryExistsAsync(Guid tenantId, Guid categoryId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> CategoryIsDescendantAsync(Guid tenantId, Guid categoryId, Guid possibleDescendantId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<CategoryDto> CreateCategoryAsync(CreateCategoryData data, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<CategoryDto?> UpdateCategoryAsync(UpdateCategoryData data, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> DisableCategoryAsync(Guid tenantId, Guid categoryId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> ActivateCategoryAsync(Guid tenantId, Guid categoryId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> DeleteCategoryAsync(Guid tenantId, Guid categoryId, Guid? requestedByUserId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PagedResult<ProductTypeDto>> ListProductTypesAsync(Guid tenantId, PageRequest pageRequest, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> ProductTypeNameExistsAsync(Guid tenantId, string name, Guid? exceptId = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ProductTypeDto> CreateProductTypeAsync(CreateProductTypeData data, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ProductTypeDto?> UpdateProductTypeAsync(UpdateProductTypeData data, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> DisableProductTypeAsync(Guid tenantId, Guid productTypeId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> ActivateProductTypeAsync(Guid tenantId, Guid productTypeId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> DeleteProductTypeAsync(Guid tenantId, Guid productTypeId, Guid? requestedByUserId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<AttributeDto>> ListProductTypeAttributesAsync(Guid tenantId, Guid productTypeId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PagedResult<AttributeDto>> ListAttributesAsync(Guid tenantId, PageRequest pageRequest, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> AttributeCodeExistsAsync(Guid tenantId, string code, Guid? exceptId = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<AttributeDto> CreateAttributeAsync(CreateAttributeData data, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<AttributeDto?> UpdateAttributeAsync(UpdateAttributeData data, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> DisableAttributeAsync(Guid tenantId, Guid attributeId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> ActivateAttributeAsync(Guid tenantId, Guid attributeId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> DeleteAttributeAsync(Guid tenantId, Guid attributeId, Guid? requestedByUserId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PagedResult<AttributeValueDto>> ListAttributeValuesAsync(Guid tenantId, Guid attributeId, PageRequest pageRequest, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> AttributeValueCodeExistsAsync(Guid tenantId, Guid attributeId, string code, Guid? exceptId = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<AttributeValueDto> CreateAttributeValueAsync(CreateAttributeValueData data, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<AttributeValueDto?> UpdateAttributeValueAsync(UpdateAttributeValueData data, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> DisableAttributeValueAsync(Guid tenantId, Guid attributeId, Guid valueId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> ActivateAttributeValueAsync(Guid tenantId, Guid attributeId, Guid valueId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> DeleteAttributeValueAsync(Guid tenantId, Guid attributeId, Guid valueId, Guid? requestedByUserId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ProductDetailsDto?> GetProductAsync(Guid tenantId, Guid productId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<ProductSummaryDto?> UpdateProductAsync(UpdateProductData data, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<ProductVariantDto>> UpdateProductVariantsAsync(UpdateProductVariantsData data, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> DisableProductAsync(Guid tenantId, Guid productId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> ActivateProductAsync(Guid tenantId, Guid productId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> DeleteProductAsync(Guid tenantId, Guid productId, Guid? requestedByUserId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<ProductVariantDto>> ListVariantsAsync(Guid tenantId, Guid productId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> DeleteProductVariantAsync(Guid tenantId, Guid productId, Guid variantId, Guid? requestedByUserId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class InMemoryInventoryRepo : IInventoryRepository
    {
        private readonly Guid _storeId;
        private readonly List<InventoryBalanceExportDto> _balances = [];

        public InMemoryInventoryRepo() : this(Guid.NewGuid()) { }

        public InMemoryInventoryRepo(Guid storeId)
        {
            _storeId = storeId;
            _balances =
            [
                new(Guid.NewGuid(), "Warehouse A", Guid.NewGuid(), "SKU-001", 100, 5, 95, 10, DateTime.UtcNow),
                new(Guid.NewGuid(), "Warehouse B", Guid.NewGuid(), "SKU-002", 50, 0, 50, 5, null)
            ];
        }

        public Task<bool> StockLocationBelongsToStoreAsync(Guid tenantId, Guid storeId, Guid stockLocationId, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> ProductVariantBelongsToTenantAsync(Guid tenantId, Guid productVariantId, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<InventoryBalanceDto?> GetBalanceAsync(Guid tenantId, Guid storeId, Guid stockLocationId, Guid productVariantId, CancellationToken cancellationToken = default) => Task.FromResult<InventoryBalanceDto?>(null);
        public Task<InventoryOperationResult> ApplyAsync(InventoryOperationData operation, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<StockMovementDto>> ListMovementsAsync(Guid tenantId, Guid storeId, Guid? productVariantId, int limit, CancellationToken cancellationToken = default)
        {
            var movements = storeId == _storeId
                ? new[] { new StockMovementDto(Guid.NewGuid(), tenantId, storeId, Guid.NewGuid(), Guid.NewGuid(), "Purchase", 100, 15m, null, "Test", Guid.NewGuid(), DateTime.UtcNow) }
                : Array.Empty<StockMovementDto>();
            return Task.FromResult<IReadOnlyCollection<StockMovementDto>>(movements);
        }
        public Task<IReadOnlyCollection<StockLocationDto>> ListStockLocationsAsync(Guid tenantId, Guid storeId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyCollection<StockLocationDto>>([]);
        public Task<PagedResult<InventoryProductSummaryDto>> ListInventoryProductsAsync(Guid tenantId, Guid storeId, InventoryProductFilters filters, PageRequest pageRequest, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PagedResult<InventoryProductSummaryDto>([], 0, pageRequest.NormalizedPage, pageRequest.NormalizedPageSize));
        public Task<InventoryProductDetailsDto?> GetInventoryProductDetailsAsync(Guid tenantId, Guid storeId, Guid productId, Guid? stockLocationId, CancellationToken cancellationToken = default) =>
            Task.FromResult<InventoryProductDetailsDto?>(null);
        public Task<IReadOnlyCollection<InventoryBalanceExportDto>> ListAllBalancesAsync(Guid tenantId, Guid storeId, CancellationToken cancellationToken = default)
        {
            var balances = storeId == _storeId ? _balances : [];
            return Task.FromResult<IReadOnlyCollection<InventoryBalanceExportDto>>(balances);
        }
    }
}
