using Arca.Application.Catalog;
using Arca.Application.Common;
using Arca.Infrastructure.Catalog;
using Arca.IntegrationTests.Database;
using Dapper;
using Npgsql;

namespace Arca.IntegrationTests.Catalog;

[Collection(DatabaseCollection.Name)]
public sealed class CatalogAuditLoggingTests(DatabaseFixture database)
{
    [Fact]
    public async Task CreateCategoryAsync_PersistsCategoryAndAuditLog()
    {
        var repository = new DapperCatalogManagementRepository(database.CreateConnectionFactory());

        var category = await repository.CreateCategoryAsync(new CreateCategoryData(
            database.TenantId,
            ParentCategoryId: null,
            Name: $"Category {Guid.NewGuid():N}",
            Slug: $"category-{Guid.NewGuid():N}",
            Description: "Integration test category",
            SortOrder: 10,
            RequestedByUserId: database.UserId));

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        var audit = await connection.QuerySingleAsync<AuditLogRow>(
            """
            SELECT action AS Action, entity_name AS EntityName, entity_id AS EntityId,
                   user_id AS UserId, tenant_id AS TenantId, old_value AS OldValue, new_value AS NewValue
            FROM audit_log
            WHERE entity_id = @CategoryId AND action = 'categories.create';
            """,
            new { CategoryId = category.Id });

        Assert.Equal("categories.create", audit.Action);
        Assert.Equal("Category", audit.EntityName);
        Assert.Equal(category.Id, audit.EntityId);
        Assert.Equal(database.UserId, audit.UserId);
        Assert.Equal(database.TenantId, audit.TenantId);
        Assert.Null(audit.OldValue);
        Assert.Contains(category.Name, audit.NewValue);
    }

    [Fact]
    public async Task UpdateProductTypeAsync_PersistsAuditLogWithOldAndNewValues()
    {
        var repository = new DapperCatalogManagementRepository(database.CreateConnectionFactory());

        var productType = await repository.CreateProductTypeAsync(new CreateProductTypeData(
            database.TenantId,
            $"Type {Guid.NewGuid():N}",
            "Before",
            database.UserId));

        var updatedName = $"Updated Type {Guid.NewGuid():N}";
        var updated = await repository.UpdateProductTypeAsync(new UpdateProductTypeData(
            database.TenantId,
            productType.Id,
            updatedName,
            "After",
            database.UserId));

        Assert.NotNull(updated);

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        var audit = await connection.QuerySingleAsync<AuditLogRow>(
            """
            SELECT action AS Action, entity_name AS EntityName, entity_id AS EntityId,
                   user_id AS UserId, tenant_id AS TenantId, old_value AS OldValue, new_value AS NewValue
            FROM audit_log
            WHERE entity_id = @ProductTypeId AND action = 'product_types.update';
            """,
            new { ProductTypeId = productType.Id });

        Assert.Equal("product_types.update", audit.Action);
        Assert.Equal("ProductType", audit.EntityName);
        Assert.Equal(productType.Id, audit.EntityId);
        Assert.Equal(database.UserId, audit.UserId);
        Assert.Equal(database.TenantId, audit.TenantId);
        Assert.Contains(productType.Name, audit.OldValue);
        Assert.Contains(updatedName, audit.NewValue);
    }

    [Fact]
    public async Task ListCategoriesAsync_AppliesSearchAndPagination()
    {
        var repository = new DapperCatalogManagementRepository(database.CreateConnectionFactory());
        var marker = Guid.NewGuid().ToString("N")[..8];

        for (var index = 1; index <= 3; index++)
        {
            await repository.CreateCategoryAsync(new CreateCategoryData(
                database.TenantId,
                ParentCategoryId: null,
                Name: $"Searchable {marker} {index}",
                Slug: $"searchable-{marker}-{index}",
                Description: null,
                SortOrder: index,
                RequestedByUserId: database.UserId));
        }

        await repository.CreateCategoryAsync(new CreateCategoryData(
            database.TenantId,
            ParentCategoryId: null,
            Name: $"Ignored {Guid.NewGuid():N}",
            Slug: $"ignored-{Guid.NewGuid():N}",
            Description: null,
            SortOrder: 99,
            RequestedByUserId: database.UserId));

        var page = await repository.ListCategoriesAsync(
            database.TenantId,
            new PageRequest(Page: 2, PageSize: 2, Search: marker));

        Assert.Equal(3, page.TotalCount);
        Assert.Equal(2, page.Page);
        Assert.Equal(2, page.PageSize);
        var category = Assert.Single(page.Items);
        Assert.Contains(marker, category.Name);
    }

    private sealed record AuditLogRow(
        string Action,
        string EntityName,
        Guid EntityId,
        Guid? UserId,
        Guid? TenantId,
        string? OldValue,
        string? NewValue);
}
