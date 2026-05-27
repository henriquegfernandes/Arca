using Arca.Application.Abstractions.Inventory;
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

    private sealed class BalanceRecord
    {
        public Guid Id { get; init; }
        public int Quantity { get; init; }
        public int ReservedQuantity { get; init; }
        public int MinimumStock { get; init; }
    }
}
