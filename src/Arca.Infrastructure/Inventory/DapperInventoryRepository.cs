using Arca.Application.Abstractions.Inventory;
using Arca.Application.Common;
using Arca.Application.Inventory;
using Arca.Infrastructure.Database;
using Dapper;

namespace Arca.Infrastructure.Inventory;

public sealed class DapperInventoryRepository(IDbConnectionFactory connectionFactory) : IInventoryRepository
{
    public async Task<bool> StockLocationBelongsToStoreAsync(
        Guid tenantId,
        Guid storeId,
        Guid stockLocationId,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            """
            SELECT EXISTS (
                SELECT 1
                FROM stock_location sl
                INNER JOIN store s ON s.id = sl.store_id
                WHERE sl.id = @StockLocationId
                  AND sl.store_id = @StoreId
                  AND s.tenant_id = @TenantId
                  AND sl.is_active = TRUE
                  AND s.is_active = TRUE
            );
            """,
            new { TenantId = tenantId, StoreId = storeId, StockLocationId = stockLocationId },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> ProductVariantBelongsToTenantAsync(
        Guid tenantId,
        Guid productVariantId,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            """
            SELECT EXISTS (
                SELECT 1
                FROM product_variant pv
                INNER JOIN product p ON p.id = pv.product_id
                WHERE pv.id = @ProductVariantId
                  AND p.tenant_id = @TenantId
            );
            """,
            new { TenantId = tenantId, ProductVariantId = productVariantId },
            cancellationToken: cancellationToken));
    }

    public async Task<InventoryBalanceDto?> GetBalanceAsync(
        Guid tenantId,
        Guid storeId,
        Guid stockLocationId,
        Guid productVariantId,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<InventoryBalanceDto>(new CommandDefinition(
            """
            SELECT
                ib.id AS Id,
                ib.stock_location_id AS StockLocationId,
                ib.product_variant_id AS ProductVariantId,
                ib.quantity AS Quantity,
                ib.reserved_quantity AS ReservedQuantity,
                (ib.quantity - ib.reserved_quantity) AS AvailableQuantity,
                ib.minimum_stock AS MinimumStock,
                ib.updated_at AS UpdatedAt
            FROM inventory_balance ib
            INNER JOIN stock_location sl ON sl.id = ib.stock_location_id
            INNER JOIN store s ON s.id = sl.store_id
            INNER JOIN product_variant pv ON pv.id = ib.product_variant_id
            INNER JOIN product p ON p.id = pv.product_id
            WHERE s.tenant_id = @TenantId
              AND s.id = @StoreId
              AND ib.stock_location_id = @StockLocationId
              AND ib.product_variant_id = @ProductVariantId
              AND p.tenant_id = @TenantId;
            """,
            new
            {
                TenantId = tenantId,
                StoreId = storeId,
                StockLocationId = stockLocationId,
                ProductVariantId = productVariantId
            },
            cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<InventoryProductSummaryDto>> ListInventoryProductsAsync(
        Guid tenantId,
        Guid storeId,
        InventoryProductFilters filters,
        PageRequest pageRequest,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        var paging = NormalizePaging(pageRequest);
        var search = string.IsNullOrWhiteSpace(filters.Search) ? paging.Search : $"%{EscapeLike(filters.Search.Trim())}%";
        var status = string.IsNullOrWhiteSpace(filters.Status) ? null : filters.Status.Trim();

        const string groupedSql = """
            WITH product_inventory AS (
                SELECT
                    p.id AS ProductId,
                    p.name AS Name,
                    p.base_sku AS BaseSku,
                    c.name AS CategoryName,
                    main_image.public_url AS MainImageUrl,
                    COALESCE(SUM(ib.quantity), 0)::int AS TotalQuantity,
                    COALESCE(SUM(ib.reserved_quantity), 0)::int AS TotalReservedQuantity,
                    COALESCE(SUM(ib.quantity - ib.reserved_quantity), 0)::int AS TotalAvailableQuantity,
                    COUNT(DISTINCT pv.id)::int AS VariantCount,
                    BOOL_OR(COALESCE(ib.quantity, 0) > 0 AND COALESCE(ib.quantity - ib.reserved_quantity, 0) <= COALESCE(ib.minimum_stock, 0)) AS HasLowStock,
                    COALESCE(SUM(ib.quantity - ib.reserved_quantity), 0) <= 0 AS IsOutOfStock,
                    p.status AS Status,
                    p.created_at AS CreatedAt
                FROM product p
                LEFT JOIN category c ON c.id = p.category_id AND c.tenant_id = @TenantId
                LEFT JOIN product_variant pv ON pv.product_id = p.id
                LEFT JOIN inventory_balance ib ON ib.product_variant_id = pv.id
                    AND EXISTS (
                        SELECT 1
                        FROM stock_location sl
                        WHERE sl.id = ib.stock_location_id
                          AND sl.store_id = @StoreId
                          AND (@StockLocationId IS NULL OR sl.id = @StockLocationId)
                    )
                LEFT JOIN LATERAL (
                    SELECT COALESCE(pi.public_url, pi.storage_path) AS public_url
                    FROM product_image pi
                    WHERE pi.product_id = p.id
                    ORDER BY pi.is_main DESC, pi.sort_order, pi.created_at
                    LIMIT 1
                ) main_image ON TRUE
                WHERE p.tenant_id = @TenantId
                  AND (@CategoryId IS NULL OR p.category_id = @CategoryId)
                  AND (@Status IS NULL OR p.status = @Status)
                  AND (
                        @Search IS NULL
                     OR p.name ILIKE @Search
                     OR p.base_sku ILIKE @Search
                     OR p.barcode ILIKE @Search
                     OR EXISTS (
                          SELECT 1 FROM product_variant sv
                          WHERE sv.product_id = p.id
                            AND (sv.sku ILIKE @Search OR sv.barcode ILIKE @Search)
                     )
                  )
                GROUP BY p.id, p.name, p.base_sku, c.name, main_image.public_url, p.status, p.created_at
            )
            """;

        var parameters = new
        {
            TenantId = tenantId,
            StoreId = storeId,
            Search = search,
            filters.CategoryId,
            Status = status,
            filters.StockLocationId,
            filters.LowStockOnly,
            filters.OutOfStockOnly,
            paging.PageSize,
            paging.Offset
        };

        var items = await connection.QueryAsync<InventoryProductSummaryDto>(new CommandDefinition(
            groupedSql + """
            SELECT ProductId, Name, BaseSku, CategoryName, MainImageUrl, TotalQuantity,
                   TotalReservedQuantity, TotalAvailableQuantity, VariantCount,
                   HasLowStock, IsOutOfStock, Status
            FROM product_inventory
            WHERE (@LowStockOnly = FALSE OR HasLowStock = TRUE)
              AND (@OutOfStockOnly = FALSE OR IsOutOfStock = TRUE)
            ORDER BY HasLowStock DESC, IsOutOfStock DESC, CreatedAt DESC
            LIMIT @PageSize OFFSET @Offset;
            """,
            parameters,
            cancellationToken: cancellationToken));

        var totalCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            groupedSql + """
            SELECT COUNT(*)::int
            FROM product_inventory
            WHERE (@LowStockOnly = FALSE OR HasLowStock = TRUE)
              AND (@OutOfStockOnly = FALSE OR IsOutOfStock = TRUE);
            """,
            parameters,
            cancellationToken: cancellationToken));

        return new PagedResult<InventoryProductSummaryDto>(items.ToArray(), totalCount, paging.Page, paging.PageSize);
    }

    public async Task<InventoryProductDetailsDto?> GetInventoryProductDetailsAsync(
        Guid tenantId,
        Guid storeId,
        Guid productId,
        Guid? stockLocationId,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        var product = await connection.QuerySingleOrDefaultAsync<ProductInventoryRecord>(new CommandDefinition(
            """
            SELECT
                p.id AS ProductId,
                p.name AS Name,
                p.base_sku AS BaseSku,
                p.description AS Description,
                main_image.public_url AS MainImageUrl,
                c.name AS CategoryName,
                p.status AS Status,
                COALESCE(SUM(ib.quantity), 0)::int AS TotalQuantity,
                COALESCE(SUM(ib.reserved_quantity), 0)::int AS TotalReservedQuantity,
                COALESCE(SUM(ib.quantity - ib.reserved_quantity), 0)::int AS TotalAvailableQuantity
            FROM product p
            LEFT JOIN category c ON c.id = p.category_id AND c.tenant_id = @TenantId
            LEFT JOIN product_variant pv ON pv.product_id = p.id
            LEFT JOIN inventory_balance ib ON ib.product_variant_id = pv.id
                AND EXISTS (
                    SELECT 1
                    FROM stock_location sl
                    WHERE sl.id = ib.stock_location_id
                      AND sl.store_id = @StoreId
                      AND (@StockLocationId IS NULL OR sl.id = @StockLocationId)
                )
            LEFT JOIN LATERAL (
                SELECT COALESCE(pi.public_url, pi.storage_path) AS public_url
                FROM product_image pi
                WHERE pi.product_id = p.id
                ORDER BY pi.is_main DESC, pi.sort_order, pi.created_at
                LIMIT 1
            ) main_image ON TRUE
            WHERE p.tenant_id = @TenantId
              AND p.id = @ProductId
            GROUP BY p.id, p.name, p.base_sku, p.description, main_image.public_url, c.name, p.status;
            """,
            new { TenantId = tenantId, StoreId = storeId, ProductId = productId, StockLocationId = stockLocationId },
            cancellationToken: cancellationToken));

        if (product is null) return null;

        var variantRows = (await connection.QueryAsync<InventoryVariantRow>(new CommandDefinition(
            """
            SELECT
                pv.id AS ProductVariantId,
                pv.sku AS Sku,
                pv.name AS Name,
                pv.barcode AS Barcode,
                COALESCE(SUM(ib.quantity), 0)::int AS Quantity,
                COALESCE(SUM(ib.reserved_quantity), 0)::int AS ReservedQuantity,
                COALESCE(SUM(ib.quantity - ib.reserved_quantity), 0)::int AS AvailableQuantity,
                COALESCE(MAX(ib.minimum_stock), 0)::int AS MinimumStock,
                pv.status AS Status
            FROM product_variant pv
            INNER JOIN product p ON p.id = pv.product_id AND p.tenant_id = @TenantId
            LEFT JOIN inventory_balance ib ON ib.product_variant_id = pv.id
                AND EXISTS (
                    SELECT 1
                    FROM stock_location sl
                    WHERE sl.id = ib.stock_location_id
                      AND sl.store_id = @StoreId
                      AND (@StockLocationId IS NULL OR sl.id = @StockLocationId)
                )
            WHERE pv.product_id = @ProductId
            GROUP BY pv.id, pv.sku, pv.name, pv.barcode, pv.status
            ORDER BY pv.sku;
            """,
            new { TenantId = tenantId, StoreId = storeId, ProductId = productId, StockLocationId = stockLocationId },
            cancellationToken: cancellationToken))).ToArray();

        var attributes = (await connection.QueryAsync<VariantAttributeRow>(new CommandDefinition(
            """
            SELECT
                pvav.product_variant_id AS ProductVariantId,
                pa.name AS AttributeName,
                pav.name AS ValueName,
                pav.code AS Code
            FROM product_variant_attribute_value pvav
            INNER JOIN product_attribute pa ON pa.id = pvav.product_attribute_id
            INNER JOIN product_attribute_value pav ON pav.id = pvav.product_attribute_value_id
            INNER JOIN product_variant pv ON pv.id = pvav.product_variant_id
            WHERE pv.product_id = @ProductId;
            """,
            new { ProductId = productId },
            cancellationToken: cancellationToken)))
            .GroupBy(row => row.ProductVariantId)
            .ToDictionary(group => group.Key, group => group.Select(row => new VariantAttributeDto(row.AttributeName, row.ValueName, row.Code)).ToArray());

        var variants = variantRows
            .Select(row => new InventoryVariantDto(
                row.ProductVariantId,
                row.Sku,
                row.Name,
                row.Barcode,
                attributes.TryGetValue(row.ProductVariantId, out var values) ? values : [],
                row.Quantity,
                row.ReservedQuantity,
                row.AvailableQuantity,
                row.MinimumStock,
                row.Quantity > 0 && row.AvailableQuantity <= row.MinimumStock,
                row.AvailableQuantity <= 0,
                row.Status))
            .ToArray();

        return new InventoryProductDetailsDto(
            product.ProductId,
            product.Name,
            product.BaseSku,
            product.Description,
            product.MainImageUrl,
            product.CategoryName,
            product.Status,
            product.TotalQuantity,
            product.TotalReservedQuantity,
            product.TotalAvailableQuantity,
            variants);
    }

    public async Task<InventoryOperationResult> ApplyAsync(
        InventoryOperationData operation,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            var now = DateTime.UtcNow;
            var current = await connection.QuerySingleOrDefaultAsync<BalanceRecord>(new CommandDefinition(
                """
                SELECT
                    id AS Id,
                    quantity AS Quantity,
                    reserved_quantity AS ReservedQuantity,
                    minimum_stock AS MinimumStock
                FROM inventory_balance
                WHERE stock_location_id = @StockLocationId
                  AND product_variant_id = @ProductVariantId
                FOR UPDATE;
                """,
                new { operation.StockLocationId, operation.ProductVariantId },
                transaction,
                cancellationToken: cancellationToken));

            var newQuantity = operation.NewQuantity ?? ((current?.Quantity ?? 0) + operation.QuantityDelta);
            if (newQuantity < 0)
            {
                throw new InvalidOperationException("Inventory balance cannot become negative.");
            }

            var balanceId = current?.Id ?? Guid.NewGuid();
            var reservedQuantity = current?.ReservedQuantity ?? 0;
            var minimumStock = operation.MinimumStock ?? current?.MinimumStock ?? 0;

            if (current is null)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO inventory_balance (
                        id, stock_location_id, product_variant_id, quantity,
                        reserved_quantity, minimum_stock, updated_at
                    )
                    VALUES (
                        @Id, @StockLocationId, @ProductVariantId, @Quantity,
                        0, @MinimumStock, @UpdatedAt
                    );
                    """,
                    new
                    {
                        Id = balanceId,
                        operation.StockLocationId,
                        operation.ProductVariantId,
                        Quantity = newQuantity,
                        MinimumStock = minimumStock,
                        UpdatedAt = now
                    },
                    transaction,
                    cancellationToken: cancellationToken));
            }
            else
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    UPDATE inventory_balance
                    SET quantity = @Quantity,
                        minimum_stock = @MinimumStock,
                        updated_at = @UpdatedAt
                    WHERE id = @Id;
                    """,
                    new
                    {
                        Id = balanceId,
                        Quantity = newQuantity,
                        MinimumStock = minimumStock,
                        UpdatedAt = now
                    },
                    transaction,
                    cancellationToken: cancellationToken));
            }

            if (operation.MovementType == "Purchase"
                && (!string.IsNullOrWhiteSpace(operation.BatchNumber) || operation.ExpirationDate is not null))
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO inventory_batch (
                        id, stock_location_id, product_variant_id, batch_number,
                        expiration_date, quantity, created_at, updated_at
                    )
                    VALUES (
                        @Id, @StockLocationId, @ProductVariantId, @BatchNumber,
                        @ExpirationDate, @Quantity, @CreatedAt, NULL
                    );
                    """,
                    new
                    {
                        Id = Guid.NewGuid(),
                        operation.StockLocationId,
                        operation.ProductVariantId,
                        operation.BatchNumber,
                        operation.ExpirationDate,
                        Quantity = Math.Abs(operation.QuantityDelta),
                        CreatedAt = now
                    },
                    transaction,
                    cancellationToken: cancellationToken));
            }

            var movementId = Guid.NewGuid();
            var movementQuantity = operation.MovementType == "Adjustment"
                ? operation.QuantityDelta
                : Math.Abs(operation.QuantityDelta);

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO stock_movement (
                    id, tenant_id, store_id, stock_location_id, product_variant_id,
                    type, quantity, unit_cost, reason, notes, user_id, created_at
                )
                VALUES (
                    @Id, @TenantId, @StoreId, @StockLocationId, @ProductVariantId,
                    @Type, @Quantity, @UnitCost, @Reason, @Notes, @UserId, @CreatedAt
                );
                """,
                new
                {
                    Id = movementId,
                    operation.TenantId,
                    operation.StoreId,
                    operation.StockLocationId,
                    operation.ProductVariantId,
                    Type = operation.MovementType,
                    Quantity = movementQuantity == 0 ? 1 : movementQuantity,
                    operation.UnitCost,
                    operation.Reason,
                    operation.Notes,
                    UserId = operation.RequestedByUserId,
                    CreatedAt = now
                },
                transaction,
                cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO audit_log (
                    id, user_id, tenant_id, store_id, action, entity_name, entity_id,
                    old_value, new_value, ip_address, user_agent, created_at
                )
                VALUES (
                    @Id, @UserId, @TenantId, @StoreId, @Action, 'InventoryBalance', @BalanceId,
                    @OldValue, @NewValue, NULL, NULL, @CreatedAt
                );
                """,
                new
                {
                    Id = Guid.NewGuid(),
                    UserId = operation.RequestedByUserId,
                    operation.TenantId,
                    operation.StoreId,
                    Action = $"inventory.{operation.MovementType.ToLowerInvariant()}",
                    BalanceId = balanceId,
                    OldValue = current is null ? null : $"Quantity={current.Quantity}; MinimumStock={current.MinimumStock}",
                    NewValue = $"Quantity={newQuantity}; MinimumStock={minimumStock}",
                    CreatedAt = now
                },
                transaction,
                cancellationToken: cancellationToken));

            transaction.Commit();

            return new InventoryOperationResult(
                new InventoryBalanceDto(
                    balanceId,
                    operation.StockLocationId,
                    operation.ProductVariantId,
                    newQuantity,
                    reservedQuantity,
                    newQuantity - reservedQuantity,
                    minimumStock,
                    now),
                new StockMovementDto(
                    movementId,
                    operation.TenantId,
                    operation.StoreId,
                    operation.StockLocationId,
                    operation.ProductVariantId,
                    operation.MovementType,
                    movementQuantity == 0 ? 1 : movementQuantity,
                    operation.UnitCost,
                    operation.Reason,
                    operation.Notes,
                    operation.RequestedByUserId,
                    now));
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<IReadOnlyCollection<StockLocationDto>> ListStockLocationsAsync(
        Guid tenantId, Guid storeId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        var locations = await connection.QueryAsync<StockLocationDto>(new CommandDefinition(
            """
            SELECT id AS Id, store_id AS StoreId, name AS Name, type AS Type,
                   is_active AS IsActive, created_at AS CreatedAt
            FROM stock_location
            WHERE store_id = @StoreId
              AND EXISTS (SELECT 1 FROM store WHERE id = @StoreId AND tenant_id = @TenantId)
            ORDER BY name;
            """,
            new { TenantId = tenantId, StoreId = storeId },
            cancellationToken: cancellationToken));

        return locations.ToArray();
    }

    public async Task<IReadOnlyCollection<StockMovementDto>> ListMovementsAsync(
        Guid tenantId,
        Guid storeId,
        Guid? productVariantId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        var movements = await connection.QueryAsync<StockMovementDto>(new CommandDefinition(
            """
            SELECT
                id AS Id,
                tenant_id AS TenantId,
                store_id AS StoreId,
                stock_location_id AS StockLocationId,
                product_variant_id AS ProductVariantId,
                type AS Type,
                quantity AS Quantity,
                unit_cost AS UnitCost,
                reason AS Reason,
                notes AS Notes,
                user_id AS UserId,
                created_at AS CreatedAt
            FROM stock_movement
            WHERE tenant_id = @TenantId
              AND store_id = @StoreId
              AND (@ProductVariantId IS NULL OR product_variant_id = @ProductVariantId)
            ORDER BY created_at DESC
            LIMIT @Limit;
            """,
            new { TenantId = tenantId, StoreId = storeId, ProductVariantId = productVariantId, Limit = limit },
            cancellationToken: cancellationToken));

        return movements.ToArray();
    }

    public async Task<IReadOnlyCollection<InventoryBalanceExportDto>> ListAllBalancesAsync(
        Guid tenantId,
        Guid storeId,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        var records = await connection.QueryAsync<InventoryBalanceExportDto>(new CommandDefinition(
            """
            SELECT
                sl.id AS StockLocationId,
                sl.name AS StockLocationName,
                ib.product_variant_id AS ProductVariantId,
                pv.sku AS VariantSku,
                ib.quantity AS Quantity,
                ib.reserved_quantity AS ReservedQuantity,
                ib.quantity - ib.reserved_quantity AS AvailableQuantity,
                ib.minimum_stock AS MinimumStock,
                ib.updated_at AS UpdatedAt
            FROM inventory_balance ib
            INNER JOIN stock_location sl ON sl.id = ib.stock_location_id
            INNER JOIN product_variant pv ON pv.id = ib.product_variant_id
            WHERE sl.store_id = @StoreId
              AND sl.is_active = TRUE
            ORDER BY sl.name, pv.sku;
            """,
            new { TenantId = tenantId, StoreId = storeId },
            cancellationToken: cancellationToken));

        return records.ToArray();
    }

    private sealed class BalanceRecord
    {
        public Guid Id { get; init; }
        public int Quantity { get; init; }
        public int ReservedQuantity { get; init; }
        public int MinimumStock { get; init; }
    }

    private sealed record ProductInventoryRecord(
        Guid ProductId,
        string Name,
        string BaseSku,
        string? Description,
        string? MainImageUrl,
        string? CategoryName,
        string Status,
        int TotalQuantity,
        int TotalReservedQuantity,
        int TotalAvailableQuantity);

    private sealed record InventoryVariantRow(
        Guid ProductVariantId,
        string Sku,
        string Name,
        string? Barcode,
        int Quantity,
        int ReservedQuantity,
        int AvailableQuantity,
        int MinimumStock,
        string Status);

    private sealed record VariantAttributeRow(
        Guid ProductVariantId,
        string AttributeName,
        string ValueName,
        string Code);

    private static (int Page, int PageSize, int Offset, string? Search) NormalizePaging(PageRequest request)
    {
        var search = request.NormalizedSearch is null ? null : $"%{EscapeLike(request.NormalizedSearch)}%";
        return (request.NormalizedPage, request.NormalizedPageSize, request.Offset, search);
    }

    private static string EscapeLike(string value) =>
        value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
