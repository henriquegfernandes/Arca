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
