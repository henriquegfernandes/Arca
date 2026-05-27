using Arca.Application.Common;
using Arca.Application.ExternalApi;
using Arca.Infrastructure.ExternalApi;
using Arca.Infrastructure.Tenancy;
using Arca.IntegrationTests.Database;
using Dapper;
using Npgsql;

namespace Arca.IntegrationTests.Admin;

[Collection(DatabaseCollection.Name)]
public sealed class AdminListPaginationTests(DatabaseFixture database)
{
    [Fact]
    public async Task ListTenantsAsync_AppliesSearchAndPagination()
    {
        var repository = new DapperTenantManagementRepository(database.CreateConnectionFactory());
        var marker = Guid.NewGuid().ToString("N")[..8];

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        for (var index = 1; index <= 3; index++)
        {
            await InsertTenantAsync(connection, $"Tenant {marker} {index}", $"tenant-{marker}-{index}");
        }

        await InsertTenantAsync(connection, $"Ignored {Guid.NewGuid():N}", $"ignored-{Guid.NewGuid():N}");

        var page = await repository.ListTenantsAsync(new PageRequest(Page: 2, PageSize: 2, Search: marker));

        Assert.Equal(3, page.TotalCount);
        Assert.Equal(2, page.Page);
        Assert.Equal(2, page.PageSize);
        var tenant = Assert.Single(page.Items);
        Assert.Contains(marker, tenant.Name);
    }

    [Fact]
    public async Task ListApiClientsAsync_AppliesSearchAndPagination()
    {
        var repository = new DapperApiClientRepository(database.CreateConnectionFactory());
        var marker = Guid.NewGuid().ToString("N")[..8];

        for (var index = 1; index <= 3; index++)
        {
            await repository.CreateAsync(
                new CreateApiClientCommand
                {
                    TenantId = database.TenantId,
                    Name = $"Client {marker} {index}",
                    Permissions = [ExternalApiPermissions.CatalogRead]
                },
                $"hash-{marker}-{index}");
        }

        await repository.CreateAsync(
            new CreateApiClientCommand
            {
                TenantId = database.TenantId,
                Name = $"Ignored {Guid.NewGuid():N}",
                Permissions = [ExternalApiPermissions.CatalogRead]
            },
            $"hash-{Guid.NewGuid():N}");

        var page = await repository.ListAsync(
            database.TenantId,
            new PageRequest(Page: 2, PageSize: 2, Search: marker));

        Assert.Equal(3, page.TotalCount);
        Assert.Equal(2, page.Page);
        Assert.Equal(2, page.PageSize);
        var client = Assert.Single(page.Items);
        Assert.Contains(marker, client.Name);
    }

    private static async Task InsertTenantAsync(NpgsqlConnection connection, string name, string slug)
    {
        var tenantId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await connection.ExecuteAsync(
            """
            INSERT INTO tenant (
                id, name, legal_name, document, slug, is_active, setup_status,
                created_at, updated_at, contact_email, phone, main_segment
            )
            VALUES (
                @TenantId, @Name, NULL, NULL, @Slug, TRUE, 'Completed',
                @CreatedAt, NULL, NULL, NULL, 'General'
            );

            INSERT INTO tenant_settings (
                id, tenant_id, currency, time_zone, default_language,
                allow_multiple_stores, allow_batch_control, allow_expiration_control,
                allow_store_specific_pricing, created_at, updated_at
            )
            VALUES (
                @TenantSettingsId, @TenantId, 'BRL', 'America/Sao_Paulo', 'pt-BR',
                TRUE, FALSE, FALSE, TRUE, @CreatedAt, NULL
            );
            """,
            new
            {
                TenantId = tenantId,
                TenantSettingsId = Guid.NewGuid(),
                Name = name,
                Slug = slug,
                CreatedAt = now
            });
    }
}
