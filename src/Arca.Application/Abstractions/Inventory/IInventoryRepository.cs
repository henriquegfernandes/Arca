using Arca.Application.Inventory;
using Arca.Application.Common;

namespace Arca.Application.Abstractions.Inventory;

public interface IInventoryRepository
{
    Task<bool> StockLocationBelongsToStoreAsync(
        Guid tenantId,
        Guid storeId,
        Guid stockLocationId,
        CancellationToken cancellationToken = default);

    Task<bool> ProductVariantBelongsToTenantAsync(
        Guid tenantId,
        Guid productVariantId,
        CancellationToken cancellationToken = default);

    Task<InventoryBalanceDto?> GetBalanceAsync(
        Guid tenantId,
        Guid storeId,
        Guid stockLocationId,
        Guid productVariantId,
        CancellationToken cancellationToken = default);

    Task<PagedResult<InventoryProductSummaryDto>> ListInventoryProductsAsync(
        Guid tenantId,
        Guid storeId,
        InventoryProductFilters filters,
        PageRequest pageRequest,
        CancellationToken cancellationToken = default);

    Task<InventoryProductDetailsDto?> GetInventoryProductDetailsAsync(
        Guid tenantId,
        Guid storeId,
        Guid productId,
        Guid? stockLocationId,
        CancellationToken cancellationToken = default);

    Task<InventoryOperationResult> ApplyAsync(
        InventoryOperationData operation,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<StockMovementDto>> ListMovementsAsync(
        Guid tenantId,
        Guid storeId,
        Guid? productVariantId,
        int limit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<StockLocationDto>> ListStockLocationsAsync(
        Guid tenantId,
        Guid storeId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<InventoryBalanceExportDto>> ListAllBalancesAsync(
        Guid tenantId,
        Guid storeId,
        CancellationToken cancellationToken = default);
}
