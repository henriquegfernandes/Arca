using Arca.Application.Security;
using Arca.Infrastructure.Database;
using Dapper;

namespace Arca.Infrastructure.Auth;

public sealed class DapperUserContextRepository(IDbConnectionFactory connectionFactory) : IUserContextRepository
{
    public async Task<UserAppContextDto?> GetAsync(
        Guid userId,
        Guid? selectedTenantId,
        Guid? selectedStoreId,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var result = await connection.QueryMultipleAsync(new CommandDefinition(
            """
            SELECT id AS Id, full_name AS FullName, email AS Email
            FROM app_user
            WHERE id = @UserId
              AND is_active = TRUE;

            SELECT
                r.name AS Name,
                r.scope AS Scope,
                ur.tenant_id AS TenantId,
                ur.store_id AS StoreId
            FROM user_role ur
            INNER JOIN role r ON r.id = ur.role_id
            WHERE ur.user_id = @UserId
              AND r.is_active = TRUE;

            SELECT DISTINCT p.name
            FROM user_role ur
            INNER JOIN role r ON r.id = ur.role_id
            INNER JOIN role_permission rp ON rp.role_id = r.id
            INNER JOIN permission p ON p.id = rp.permission_id
            WHERE ur.user_id = @UserId
              AND r.is_active = TRUE;
            """,
            new { UserId = userId },
            cancellationToken: cancellationToken));

        var user = await result.ReadSingleOrDefaultAsync<UserContextRow>();
        if (user is null)
        {
            return null;
        }

        var roles = (await result.ReadAsync<UserRoleContextRow>()).ToArray();
        var isSuperAdmin = roles.Any(role =>
            role.Scope.Equals("System", StringComparison.OrdinalIgnoreCase)
            && role.Name.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase));
        var permissions = isSuperAdmin
            ? KnownPermissions.All.Select(permission => permission.Name).ToArray()
            : (await result.ReadAsync<string>()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(permission => permission).ToArray();

        if (isSuperAdmin)
        {
            _ = await result.ReadAsync<string>();
        }

        var tenants = await LoadTenantsAsync(userId, isSuperAdmin, cancellationToken);
        var currentTenant = ResolveCurrentTenant(tenants, selectedTenantId, isSuperAdmin);
        var hasTenantScopeForCurrentTenant = currentTenant is not null
            && roles.Any(role =>
                role.Scope.Equals("Tenant", StringComparison.OrdinalIgnoreCase)
                && role.TenantId == currentTenant.Id);

        var stores = currentTenant is null
            ? []
            : await LoadStoresAsync(userId, currentTenant.Id, isSuperAdmin || hasTenantScopeForCurrentTenant, cancellationToken);
        var currentStore = ResolveCurrentStore(stores, selectedStoreId, currentTenant?.PrimaryStoreId);

        return new UserAppContextDto(
            new CurrentUserContextDto(
                user.Id,
                user.FullName,
                user.Email,
                isSuperAdmin,
                roles.Select(role => role.Name).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(role => role).ToArray(),
                permissions),
            currentTenant,
            currentStore,
            tenants,
            stores);
    }

    private async Task<IReadOnlyCollection<TenantContextDto>> LoadTenantsAsync(
        Guid userId,
        bool isSuperAdmin,
        CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var tenants = await connection.QueryAsync<TenantContextDto>(new CommandDefinition(
            isSuperAdmin
                ? """
                  SELECT id AS Id, name AS Name, slug AS Slug, primary_store_id AS PrimaryStoreId
                  FROM tenant
                  WHERE is_active = TRUE
                  ORDER BY name;
                  """
                : """
                  SELECT DISTINCT t.id AS Id, t.name AS Name, t.slug AS Slug, t.primary_store_id AS PrimaryStoreId
                  FROM tenant t
                  INNER JOIN user_tenant ut ON ut.tenant_id = t.id
                  WHERE ut.user_id = @UserId
                    AND ut.is_active = TRUE
                    AND t.is_active = TRUE
                  ORDER BY t.name;
                  """,
            new { UserId = userId },
            cancellationToken: cancellationToken));

        return tenants.ToArray();
    }

    private async Task<IReadOnlyCollection<StoreContextDto>> LoadStoresAsync(
        Guid userId,
        Guid tenantId,
        bool canSeeAllTenantStores,
        CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var stores = await connection.QueryAsync<StoreContextDto>(new CommandDefinition(
            canSeeAllTenantStores
                ? """
                  SELECT id AS Id, tenant_id AS TenantId, name AS Name, code AS Code
                  FROM store
                  WHERE tenant_id = @TenantId
                    AND is_active = TRUE
                  ORDER BY name;
                  """
                : """
                  SELECT DISTINCT s.id AS Id, s.tenant_id AS TenantId, s.name AS Name, s.code AS Code
                  FROM store s
                  INNER JOIN user_store us ON us.store_id = s.id
                  WHERE us.user_id = @UserId
                    AND us.tenant_id = @TenantId
                    AND us.is_active = TRUE
                    AND s.is_active = TRUE
                  ORDER BY s.name;
                  """,
            new { UserId = userId, TenantId = tenantId },
            cancellationToken: cancellationToken));

        return stores.ToArray();
    }

    private static TenantContextDto? ResolveCurrentTenant(
        IReadOnlyCollection<TenantContextDto> tenants,
        Guid? selectedTenantId,
        bool isSuperAdmin)
    {
        if (selectedTenantId is not null)
        {
            return tenants.FirstOrDefault(tenant => tenant.Id == selectedTenantId.Value);
        }

        return !isSuperAdmin && tenants.Count == 1 ? tenants.Single() : null;
    }

    private static StoreContextDto? ResolveCurrentStore(
        IReadOnlyCollection<StoreContextDto> stores,
        Guid? selectedStoreId,
        Guid? primaryStoreId)
    {
        if (selectedStoreId is not null)
        {
            return stores.FirstOrDefault(store => store.Id == selectedStoreId.Value);
        }

        if (primaryStoreId is not null)
        {
            var primaryStore = stores.FirstOrDefault(store => store.Id == primaryStoreId.Value);
            if (primaryStore is not null)
            {
                return primaryStore;
            }
        }

        return stores.Count == 1 ? stores.Single() : null;
    }

    private sealed class UserContextRow
    {
        public Guid Id { get; init; }
        public string FullName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
    }

    private sealed class UserRoleContextRow
    {
        public string Name { get; init; } = string.Empty;
        public string Scope { get; init; } = string.Empty;
        public Guid? TenantId { get; init; }
        public Guid? StoreId { get; init; }
    }
}
