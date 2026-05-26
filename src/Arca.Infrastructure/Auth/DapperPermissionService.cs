using Arca.Application.Security;
using Arca.Infrastructure.Database;
using Dapper;

namespace Arca.Infrastructure.Auth;

public sealed class DapperPermissionService(IDbConnectionFactory connectionFactory) : IPermissionService
{
    public async Task<bool> HasPermissionAsync(
        Guid userId,
        string permission,
        Guid? tenantId,
        Guid? storeId,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            """
            SELECT EXISTS (
                SELECT 1
                FROM user_role ur
                INNER JOIN role r ON r.id = ur.role_id
                INNER JOIN role_permission rp ON rp.role_id = r.id
                INNER JOIN permission p ON p.id = rp.permission_id
                WHERE ur.user_id = @UserId
                  AND r.is_active = TRUE
                  AND p.name = @Permission
                  AND (
                        (r.scope = 'System' AND ur.tenant_id IS NULL AND ur.store_id IS NULL)
                     OR (r.scope = 'Tenant' AND ur.tenant_id = @TenantId AND ur.store_id IS NULL)
                     OR (r.scope = 'Store' AND ur.tenant_id = @TenantId AND ur.store_id = @StoreId)
                  )
            );
            """,
            new { UserId = userId, Permission = permission, TenantId = tenantId, StoreId = storeId },
            cancellationToken: cancellationToken));
    }
}
