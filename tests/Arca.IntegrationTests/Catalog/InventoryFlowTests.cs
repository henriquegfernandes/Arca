using Arca.Infrastructure.Catalog;
using Arca.Infrastructure.Inventory;
using Arca.Infrastructure.Tenancy;
using Arca.IntegrationTests.Database;
using Dapper;
using Npgsql;

namespace Arca.IntegrationTests.Catalog;

[Collection(DatabaseCollection.Name)]
public sealed class InventoryFlowTests(DatabaseFixture database)
{
    [Fact]
    public async Task EntryAndExit_PersistsBalanceAndMovement()
    {
        var invRepo = new DapperInventoryRepository(database.CreateConnectionFactory());

        var storeId = await SeedStoreAsync();
        var locationId = await SeedStockLocationAsync(storeId);
        var variantId = await SeedProductVariantAsync();

        var entryResult = await invRepo.ApplyAsync(new(
            database.TenantId, storeId, locationId, variantId,
            "Purchase", +100, null, null, 15.50m,
            "Initial stock", "Test entry", null, null, database.UserId));

        Assert.Equal(100, entryResult.Balance.Quantity);
        Assert.Equal(100, entryResult.Balance.AvailableQuantity);
        Assert.Equal(0, entryResult.Balance.ReservedQuantity);

        var balanceCheck = await invRepo.GetBalanceAsync(
            database.TenantId, storeId, locationId, variantId);
        Assert.NotNull(balanceCheck);
        Assert.Equal(100, balanceCheck.Quantity);

        var exitResult = await invRepo.ApplyAsync(new(
            database.TenantId, storeId, locationId, variantId,
            "Sale", -30, null, null, null,
            "Customer order", "Test exit", null, null, database.UserId));

        Assert.Equal(70, exitResult.Balance.Quantity);
        Assert.Equal(70, exitResult.Balance.AvailableQuantity);

        var movements = await invRepo.ListMovementsAsync(
            database.TenantId, storeId, variantId, 10);
        Assert.Equal(2, movements.Count);
        Assert.Contains(movements, m => m.Type == "Purchase" && m.Quantity == 100);
        Assert.Contains(movements, m => m.Type == "Sale" && m.Quantity == 30);

        var locations = await invRepo.ListStockLocationsAsync(database.TenantId, storeId);
        Assert.NotEmpty(locations);
        Assert.Contains(locations, l => l.Id == locationId);

        var allBalances = await invRepo.ListAllBalancesAsync(database.TenantId, storeId);
        var balance = Assert.Single(allBalances);
        Assert.Equal(70, balance.Quantity);
        Assert.Equal(70, balance.AvailableQuantity);
    }

    [Fact]
    public async Task ListAllBalancesAsync_ReturnsEmptyForNewStore()
    {
        var invRepo = new DapperInventoryRepository(database.CreateConnectionFactory());
        var storeId = await SeedStoreAsync();

        var balances = await invRepo.ListAllBalancesAsync(database.TenantId, storeId);
        Assert.Empty(balances);
    }

    private async Task<Guid> SeedStoreAsync()
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        var storeId = Guid.NewGuid();
        var code = $"TST{Guid.NewGuid():N}"[..10];
        var now = DateTime.UtcNow;

        await connection.ExecuteAsync(
            """
            INSERT INTO store (id, tenant_id, name, code, type, is_active, created_at)
            VALUES (@Id, @TenantId, 'Test Store', @Code, 'Physical', TRUE, @Now);
            """,
            new { Id = storeId, TenantId = database.TenantId, Code = code, Now = now });

        return storeId;
    }

    private async Task<Guid> SeedStockLocationAsync(Guid storeId)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        var locationId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await connection.ExecuteAsync(
            """
            INSERT INTO stock_location (id, store_id, name, type, is_active, created_at)
            VALUES (@Id, @StoreId, 'Main Warehouse', 'Warehouse', TRUE, @Now);
            """,
            new { Id = locationId, StoreId = storeId, Now = now });

        return locationId;
    }

    private async Task<Guid> SeedProductVariantAsync()
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        var productId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await connection.ExecuteAsync(
            """
            INSERT INTO product (id, tenant_id, name, slug, base_sku, status, created_at)
            VALUES (@ProductId, @TenantId, 'Test Product', 'test-product', 'TST', 'Active', @Now);

            INSERT INTO product_variant (id, product_id, sku, name, default_sale_price, status, created_at)
            VALUES (@VariantId, @ProductId, 'TST-001', 'Test Product', 0, 'Active', @Now);
            """,
            new { ProductId = productId, TenantId = database.TenantId, VariantId = variantId, Now = now });

        return variantId;
    }
}
