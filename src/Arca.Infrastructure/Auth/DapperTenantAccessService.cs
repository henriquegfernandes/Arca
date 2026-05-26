using Arca.Application.Security;
using Arca.Infrastructure.Database;
using Dapper;

namespace Arca.Infrastructure.Auth;

public sealed class DapperTenantAccessService(IDbConnectionFactory connectionFactory) : ITenantAccessService
{
    public async Task<bool> UserHasAccessToTenantAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            """
            SELECT EXISTS (
                SELECT 1
                FROM user_role ur
                INNER JOIN role r ON r.id = ur.role_id
                WHERE ur.user_id = @UserId
                  AND r.scope = 'System'
                  AND r.normalized_name = 'SUPERADMIN'
                  AND r.is_active = TRUE
            )
            OR EXISTS (
                SELECT 1
                FROM user_tenant
                WHERE user_id = @UserId
                  AND tenant_id = @TenantId
                  AND is_active = TRUE
            );
            """,
            new { UserId = userId, TenantId = tenantId },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> UserHasAccessToStoreAsync(
        Guid userId,
        Guid tenantId,
        Guid storeId,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            """
            SELECT EXISTS (
                SELECT 1
                FROM user_role ur
                INNER JOIN role r ON r.id = ur.role_id
                WHERE ur.user_id = @UserId
                  AND r.scope = 'System'
                  AND r.normalized_name = 'SUPERADMIN'
                  AND r.is_active = TRUE
            )
            OR EXISTS (
                SELECT 1
                FROM user_store
                WHERE user_id = @UserId
                  AND tenant_id = @TenantId
                  AND store_id = @StoreId
                  AND is_active = TRUE
            );
            """,
            new { UserId = userId, TenantId = tenantId, StoreId = storeId },
            cancellationToken: cancellationToken));
    }
}
