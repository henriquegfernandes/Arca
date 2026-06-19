using Arca.Application.Abstractions.Dashboard;
using Arca.Application.Dashboard;
using Arca.Infrastructure.Database;
using Dapper;

namespace Arca.Infrastructure.Dashboard;

public sealed class DapperDashboardRepository(IDbConnectionFactory connectionFactory) : IDashboardRepository
{
    public async Task<DashboardSummaryDto> GetSummaryAsync(
        DashboardSummaryQuery query,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        var parameters = new { query.TenantId, query.StoreId };

        var counts = await connection.QuerySingleAsync<DashboardCounts>(new CommandDefinition(
            """
            SELECT
                (
                    SELECT COUNT(*)::int
                    FROM tenant t
                    WHERE t.is_active = TRUE
                      AND (@TenantId IS NULL OR t.id = @TenantId)
                ) AS TenantCount,
                (
                    SELECT COUNT(*)::int
                    FROM store s
                    WHERE s.is_active = TRUE
                      AND (@TenantId IS NULL OR s.tenant_id = @TenantId)
                      AND (@StoreId IS NULL OR s.id = @StoreId)
                ) AS StoreCount,
                (
                    SELECT COUNT(DISTINCT u.id)::int
                    FROM app_user u
                    LEFT JOIN user_tenant ut ON ut.user_id = u.id
                    LEFT JOIN user_store us ON us.user_id = u.id
                    WHERE u.is_active = TRUE
                      AND (
                            @TenantId IS NULL
                         OR (ut.tenant_id = @TenantId AND ut.is_active = TRUE)
                         OR (us.tenant_id = @TenantId AND us.is_active = TRUE)
                      )
                      AND (@StoreId IS NULL OR (us.store_id = @StoreId AND us.is_active = TRUE))
                ) AS UserCount,
                (
                    SELECT COUNT(*)::int
                    FROM product p
                    WHERE (@TenantId IS NULL OR p.tenant_id = @TenantId)
                      AND p.status <> 'Inactive'
                ) AS ProductCount,
                (
                    SELECT COUNT(*)::int
                    FROM product_variant pv
                    INNER JOIN product p ON p.id = pv.product_id
                    WHERE (@TenantId IS NULL OR p.tenant_id = @TenantId)
                      AND p.status <> 'Inactive'
                      AND pv.status <> 'Inactive'
                ) AS VariantCount,
                (
                    SELECT COUNT(*)::int
                    FROM api_client ac
                    WHERE ac.is_active = TRUE
                      AND (@TenantId IS NULL OR ac.tenant_id = @TenantId)
                      AND (@StoreId IS NULL OR ac.store_id IS NULL OR ac.store_id = @StoreId)
                ) AS ApiClientCount;
            """,
            parameters,
            cancellationToken: cancellationToken));

        var inventory = await connection.QuerySingleAsync<DashboardInventorySnapshotDto>(new CommandDefinition(
            """
            WITH product_inventory AS (
                SELECT
                    p.id,
                    COALESCE(SUM(ib.quantity), 0)::int AS Quantity,
                    COALESCE(SUM(ib.reserved_quantity), 0)::int AS ReservedQuantity,
                    COALESCE(SUM(ib.quantity - ib.reserved_quantity), 0)::int AS AvailableQuantity,
                    BOOL_OR(COALESCE(ib.quantity, 0) > 0 AND COALESCE(ib.quantity - ib.reserved_quantity, 0) <= COALESCE(ib.minimum_stock, 0)) AS HasLowStock
                FROM product p
                LEFT JOIN product_variant pv ON pv.product_id = p.id
                LEFT JOIN inventory_balance ib ON ib.product_variant_id = pv.id
                    AND EXISTS (
                        SELECT 1
                        FROM stock_location sl
                        INNER JOIN store s ON s.id = sl.store_id
                        WHERE sl.id = ib.stock_location_id
                          AND (@TenantId IS NULL OR s.tenant_id = @TenantId)
                          AND (@StoreId IS NULL OR s.id = @StoreId)
                    )
                WHERE (@TenantId IS NULL OR p.tenant_id = @TenantId)
                  AND p.status <> 'Inactive'
                GROUP BY p.id
            )
            SELECT
                COALESCE(SUM(Quantity), 0)::int AS TotalQuantity,
                COALESCE(SUM(ReservedQuantity), 0)::int AS ReservedQuantity,
                COALESCE(SUM(AvailableQuantity), 0)::int AS AvailableQuantity,
                COUNT(*) FILTER (WHERE HasLowStock = TRUE)::int AS LowStockProducts,
                COUNT(*) FILTER (WHERE AvailableQuantity <= 0)::int AS OutOfStockProducts
            FROM product_inventory;
            """,
            parameters,
            cancellationToken: cancellationToken));

        var recentMovements = (await connection.QueryAsync<DashboardRecentMovementDto>(new CommandDefinition(
            """
            SELECT
                sm.id AS Id,
                sm.type AS Type,
                sm.quantity AS Quantity,
                p.name AS ProductName,
                pv.sku AS VariantSku,
                s.name AS StoreName,
                sm.created_at AS CreatedAt
            FROM stock_movement sm
            INNER JOIN store s ON s.id = sm.store_id
            INNER JOIN product_variant pv ON pv.id = sm.product_variant_id
            INNER JOIN product p ON p.id = pv.product_id
            WHERE (@TenantId IS NULL OR sm.tenant_id = @TenantId)
              AND (@StoreId IS NULL OR sm.store_id = @StoreId)
            ORDER BY sm.created_at DESC
            LIMIT 8;
            """,
            parameters,
            cancellationToken: cancellationToken)).ConfigureAwait(false)).ToArray();

        return new DashboardSummaryDto(
            ResolveScope(query),
            BuildMetrics(counts),
            inventory,
            recentMovements);
    }

    private static string ResolveScope(DashboardSummaryQuery query)
    {
        if (query.TenantId is null) return "Platform";
        return query.StoreId is null ? "Tenant" : "Store";
    }

    private static IReadOnlyCollection<DashboardMetricDto> BuildMetrics(DashboardCounts counts) =>
    [
        new("tenants", "Tenants", counts.TenantCount),
        new("stores", "Stores", counts.StoreCount),
        new("users", "Users", counts.UserCount),
        new("products", "Products", counts.ProductCount),
        new("variants", "Variants", counts.VariantCount),
        new("apiClients", "API Keys", counts.ApiClientCount)
    ];

    private sealed class DashboardCounts
    {
        public int TenantCount { get; init; }
        public int StoreCount { get; init; }
        public int UserCount { get; init; }
        public int ProductCount { get; init; }
        public int VariantCount { get; init; }
        public int ApiClientCount { get; init; }
    }
}
