namespace Arca.Application.Inventory;

public sealed class RegisterStockEntryCommand
{
    public Guid TenantId { get; init; }
    public Guid StoreId { get; init; }
    public Guid StockLocationId { get; init; }
    public Guid ProductVariantId { get; init; }
    public int Quantity { get; init; }
    public decimal? UnitCost { get; init; }
    public string? Reason { get; init; }
    public string? Notes { get; init; }
    public string? BatchNumber { get; init; }
    public DateTime? ExpirationDate { get; init; }
    public Guid? RequestedByUserId { get; init; }
}

public sealed class RegisterStockExitCommand
{
    public Guid TenantId { get; init; }
    public Guid StoreId { get; init; }
    public Guid StockLocationId { get; init; }
    public Guid ProductVariantId { get; init; }
    public int Quantity { get; init; }
    public string MovementType { get; init; } = "Sale";
    public string? Reason { get; init; }
    public string? Notes { get; init; }
    public Guid? RequestedByUserId { get; init; }
}

public sealed class AdjustStockCommand
{
    public Guid TenantId { get; init; }
    public Guid StoreId { get; init; }
    public Guid StockLocationId { get; init; }
    public Guid ProductVariantId { get; init; }
    public int NewQuantity { get; init; }
    public int? MinimumStock { get; init; }
    public string? Reason { get; init; }
    public string? Notes { get; init; }
    public Guid? RequestedByUserId { get; init; }
}

public sealed record InventoryBalanceDto(
    Guid Id,
    Guid StockLocationId,
    Guid ProductVariantId,
    int Quantity,
    int ReservedQuantity,
    int AvailableQuantity,
    int MinimumStock,
    DateTime? UpdatedAt);

public sealed record StockMovementDto(
    Guid Id,
    Guid TenantId,
    Guid StoreId,
    Guid StockLocationId,
    Guid ProductVariantId,
    string Type,
    int Quantity,
    decimal? UnitCost,
    string? Reason,
    string? Notes,
    Guid? UserId,
    DateTime CreatedAt);

public sealed record InventoryOperationResult(
    InventoryBalanceDto Balance,
    StockMovementDto Movement);

public sealed record StockLocationDto(
    Guid Id,
    Guid StoreId,
    string Name,
    string Type,
    bool IsActive,
    DateTime CreatedAt);

public sealed record InventoryBalanceExportDto(
    Guid StockLocationId,
    string StockLocationName,
    Guid ProductVariantId,
    string VariantSku,
    int Quantity,
    int ReservedQuantity,
    int AvailableQuantity,
    int MinimumStock,
    DateTime? UpdatedAt);

public sealed record InventoryProductFilters(
    string? Search,
    Guid? CategoryId,
    string? Status,
    bool LowStockOnly,
    bool OutOfStockOnly,
    Guid? StockLocationId);

public sealed record InventoryProductSummaryDto(
    Guid ProductId,
    string Name,
    string BaseSku,
    string? CategoryName,
    string? MainImageUrl,
    int TotalQuantity,
    int TotalReservedQuantity,
    int TotalAvailableQuantity,
    int VariantCount,
    bool HasLowStock,
    bool IsOutOfStock,
    string Status);

public sealed record InventoryProductDetailsDto(
    Guid ProductId,
    string Name,
    string BaseSku,
    string? Description,
    string? MainImageUrl,
    string? CategoryName,
    string Status,
    int TotalQuantity,
    int TotalReservedQuantity,
    int TotalAvailableQuantity,
    IReadOnlyCollection<InventoryVariantDto> Variants);

public sealed record InventoryVariantDto(
    Guid ProductVariantId,
    string Sku,
    string Name,
    string? Barcode,
    IReadOnlyCollection<VariantAttributeDto> Attributes,
    int Quantity,
    int ReservedQuantity,
    int AvailableQuantity,
    int MinimumStock,
    bool IsLowStock,
    bool IsOutOfStock,
    string Status);

public sealed record VariantAttributeDto(
    string AttributeName,
    string ValueName,
    string Code);

public sealed class StockMovementRequest
{
    public string Type { get; init; } = "Entry";
    public Guid TenantId { get; init; }
    public Guid StoreId { get; init; }
    public Guid StockLocationId { get; init; }
    public List<StockMovementItemRequest> Items { get; init; } = [];
    public string? Reason { get; init; }
    public string? Notes { get; init; }
    public Guid? RequestedByUserId { get; init; }
}

public sealed class StockMovementItemRequest
{
    public Guid ProductVariantId { get; init; }
    public Guid? StockLocationId { get; init; }
    public int Quantity { get; init; }
    public decimal? UnitCost { get; init; }
}

public sealed record InventoryOperationData(
    Guid TenantId,
    Guid StoreId,
    Guid StockLocationId,
    Guid ProductVariantId,
    string MovementType,
    int QuantityDelta,
    int? NewQuantity,
    int? MinimumStock,
    decimal? UnitCost,
    string? Reason,
    string? Notes,
    string? BatchNumber,
    DateTime? ExpirationDate,
    Guid? RequestedByUserId);
