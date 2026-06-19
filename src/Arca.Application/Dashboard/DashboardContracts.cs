namespace Arca.Application.Dashboard;

public sealed record DashboardSummaryDto(
    string Scope,
    IReadOnlyCollection<DashboardMetricDto> Metrics,
    DashboardInventorySnapshotDto Inventory,
    IReadOnlyCollection<DashboardRecentMovementDto> RecentMovements);

public sealed record DashboardMetricDto(
    string Key,
    string Label,
    decimal Value,
    string? Hint = null);

public sealed record DashboardInventorySnapshotDto(
    int TotalQuantity,
    int ReservedQuantity,
    int AvailableQuantity,
    int LowStockProducts,
    int OutOfStockProducts);

public sealed record DashboardRecentMovementDto(
    Guid Id,
    string Type,
    int Quantity,
    string ProductName,
    string VariantSku,
    string? StoreName,
    DateTime CreatedAt);

public sealed record DashboardSummaryQuery(
    Guid? TenantId,
    Guid? StoreId,
    bool IsSuperAdmin);
