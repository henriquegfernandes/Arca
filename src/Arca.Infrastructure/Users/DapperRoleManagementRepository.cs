using Arca.Application.Abstractions.Users;
using Arca.Application.Common;
using Arca.Application.Users;
using Arca.Infrastructure.Database;
using Dapper;

namespace Arca.Infrastructure.Users;

public sealed class DapperRoleManagementRepository(IDbConnectionFactory connectionFactory) : IRoleManagementRepository
{
    public async Task<IReadOnlyCollection<PermissionDto>> ListPermissionsAsync(
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        var permissions = await connection.QueryAsync<PermissionDto>(new CommandDefinition(
            """
            SELECT
                id AS Id,
                name AS Name,
                COALESCE(description, '') AS Description,
                module AS Module
            FROM permission
            ORDER BY module, name;
            """,
            cancellationToken: cancellationToken));

        return permissions.ToArray();
    }

    public async Task<PagedResult<RoleDetailsDto>> ListRolesAsync(
        Guid? tenantId,
        PageRequest pageRequest,
        CancellationToken cancellationToken = default)
    {
        var paging = NormalizePaging(pageRequest);
        using var connection = connectionFactory.CreateConnection();
        using var result = await connection.QueryMultipleAsync(new CommandDefinition(
            """
            WITH filtered_roles AS (
                SELECT DISTINCT r.id, r.scope, r.name
                FROM role r
                LEFT JOIN role_permission rp ON rp.role_id = r.id
                LEFT JOIN permission p ON p.id = rp.permission_id
                WHERE (
                        r.tenant_id IS NULL
                     OR (@TenantId IS NOT NULL AND r.tenant_id = @TenantId)
                )
                  AND (
                        @Search IS NULL
                     OR r.name ILIKE @Search
                     OR r.scope ILIKE @Search
                     OR r.description ILIKE @Search
                     OR p.name ILIKE @Search
                  )
            )
            SELECT COUNT(*)::int FROM filtered_roles;

            WITH filtered_roles AS (
                SELECT DISTINCT r.id, r.scope, r.name
                FROM role r
                LEFT JOIN role_permission rp ON rp.role_id = r.id
                LEFT JOIN permission p ON p.id = rp.permission_id
                WHERE (
                        r.tenant_id IS NULL
                     OR (@TenantId IS NOT NULL AND r.tenant_id = @TenantId)
                )
                  AND (
                        @Search IS NULL
                     OR r.name ILIKE @Search
                     OR r.scope ILIKE @Search
                     OR r.description ILIKE @Search
                     OR p.name ILIKE @Search
                  )
            ),
            selected_roles AS (
                SELECT id
                FROM filtered_roles
                ORDER BY scope, name
                LIMIT @PageSize OFFSET @Offset
            )
            SELECT
                r.id AS Id,
                r.tenant_id AS TenantId,
                r.name AS Name,
                r.normalized_name AS NormalizedName,
                r.description AS Description,
                r.scope AS Scope,
                r.is_system_role AS IsSystemRole,
                r.is_active AS IsActive,
                r.created_at AS CreatedAt,
                r.updated_at AS UpdatedAt,
                p.name AS Permission
            FROM role r
            INNER JOIN selected_roles sr ON sr.id = r.id
            LEFT JOIN role_permission rp ON rp.role_id = r.id
            LEFT JOIN permission p ON p.id = rp.permission_id
            ORDER BY r.scope, r.name, p.name;
            """,
            new { TenantId = tenantId, paging.Search, paging.PageSize, paging.Offset },
            cancellationToken: cancellationToken));

        var totalCount = await result.ReadSingleAsync<int>();
        var rows = await result.ReadAsync<RolePermissionRow>();
        return new PagedResult<RoleDetailsDto>(BuildRoles(rows), totalCount, paging.Page, paging.PageSize);
    }

    public async Task<RoleDetailsDto?> GetRoleAsync(
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<RolePermissionRow>(new CommandDefinition(
            """
            SELECT
                r.id AS Id,
                r.tenant_id AS TenantId,
                r.name AS Name,
                r.normalized_name AS NormalizedName,
                r.description AS Description,
                r.scope AS Scope,
                r.is_system_role AS IsSystemRole,
                r.is_active AS IsActive,
                r.created_at AS CreatedAt,
                r.updated_at AS UpdatedAt,
                p.name AS Permission
            FROM role r
            LEFT JOIN role_permission rp ON rp.role_id = r.id
            LEFT JOIN permission p ON p.id = rp.permission_id
            WHERE r.id = @RoleId
            ORDER BY p.name;
            """,
            new { RoleId = roleId },
            cancellationToken: cancellationToken));

        return BuildRoles(rows).SingleOrDefault();
    }

    public async Task<bool> TenantExistsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM tenant WHERE id = @TenantId AND is_active = TRUE);",
            new { TenantId = tenantId },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> RoleNameExistsAsync(
        Guid? tenantId,
        string normalizedName,
        Guid? exceptRoleId = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            """
            SELECT EXISTS (
                SELECT 1
                FROM role
                WHERE normalized_name = @NormalizedName
                  AND (
                        (@TenantId IS NULL AND tenant_id IS NULL)
                     OR (@TenantId IS NOT NULL AND tenant_id = @TenantId)
                  )
                  AND (@ExceptRoleId IS NULL OR id <> @ExceptRoleId)
            );
            """,
            new { TenantId = tenantId, NormalizedName = normalizedName, ExceptRoleId = exceptRoleId },
            cancellationToken: cancellationToken));
    }

    public async Task<RoleDetailsDto> CreateRoleAsync(
        CreateRoleData data,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            var now = DateTime.UtcNow;
            var roleId = Guid.NewGuid();

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO role (
                    id, tenant_id, name, normalized_name, description, scope,
                    is_system_role, is_active, created_at, updated_at
                )
                VALUES (
                    @Id, @TenantId, @Name, @NormalizedName, @Description, @Scope,
                    FALSE, TRUE, @CreatedAt, NULL
                );
                """,
                new
                {
                    Id = roleId,
                    data.TenantId,
                    data.Name,
                    data.NormalizedName,
                    data.Description,
                    data.Scope,
                    CreatedAt = now
                },
                transaction,
                cancellationToken: cancellationToken));

            await ReplacePermissionsAsync(connection, transaction, roleId, data.Permissions, cancellationToken);
            await InsertAuditLogAsync(
                connection,
                transaction,
                data.RequestedByUserId,
                data.TenantId,
                "roles.create",
                roleId,
                $"Name={data.Name}; Scope={data.Scope}; Permissions={string.Join(",", data.Permissions)}",
                data.IpAddress,
                data.UserAgent,
                now,
                cancellationToken);

            transaction.Commit();

            return new RoleDetailsDto(
                roleId,
                data.TenantId,
                data.Name,
                data.NormalizedName,
                data.Description,
                data.Scope,
                false,
                true,
                data.Permissions,
                now,
                null);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<RoleDetailsDto?> UpdateRolePermissionsAsync(
        UpdateRolePermissionsData data,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            var roleExists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                "SELECT EXISTS (SELECT 1 FROM role WHERE id = @RoleId AND is_active = TRUE);",
                new { data.RoleId },
                transaction,
                cancellationToken: cancellationToken));

            if (!roleExists)
            {
                transaction.Commit();
                return null;
            }

            await ReplacePermissionsAsync(connection, transaction, data.RoleId, data.Permissions, cancellationToken);

            var now = DateTime.UtcNow;
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE role SET updated_at = @UpdatedAt WHERE id = @RoleId;",
                new { data.RoleId, UpdatedAt = now },
                transaction,
                cancellationToken: cancellationToken));

            await InsertAuditLogAsync(
                connection,
                transaction,
                data.RequestedByUserId,
                data.TenantId,
                "roles.manage_permissions",
                data.RoleId,
                $"Permissions={string.Join(",", data.Permissions)}",
                data.IpAddress,
                data.UserAgent,
                now,
                cancellationToken);

            transaction.Commit();
            return await GetRoleAsync(data.RoleId, cancellationToken);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<bool> DisableRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE role
            SET is_active = FALSE,
                updated_at = @UpdatedAt
            WHERE id = @RoleId
              AND is_system_role = FALSE
              AND is_active = TRUE;
            """,
            new { RoleId = roleId, UpdatedAt = DateTime.UtcNow },
            cancellationToken: cancellationToken));

        return affected > 0;
    }

    private static async Task ReplacePermissionsAsync(
        System.Data.IDbConnection connection,
        System.Data.IDbTransaction transaction,
        Guid roleId,
        IReadOnlyCollection<string> permissions,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM role_permission WHERE role_id = @RoleId;",
            new { RoleId = roleId },
            transaction,
            cancellationToken: cancellationToken));

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO role_permission (role_id, permission_id)
            SELECT @RoleId, id
            FROM permission
            WHERE name = ANY(@Permissions)
            ON CONFLICT (role_id, permission_id) DO NOTHING;
            """,
            new { RoleId = roleId, Permissions = permissions },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static Task InsertAuditLogAsync(
        System.Data.IDbConnection connection,
        System.Data.IDbTransaction transaction,
        Guid? userId,
        Guid? tenantId,
        string action,
        Guid roleId,
        string newValue,
        string? ipAddress,
        string? userAgent,
        DateTime createdAt,
        CancellationToken cancellationToken)
    {
        return connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO audit_log (
                id, user_id, tenant_id, store_id, action, entity_name, entity_id,
                old_value, new_value, ip_address, user_agent, created_at
            )
            VALUES (
                @Id, @UserId, @TenantId, NULL, @Action, 'Role', @RoleId,
                NULL, @NewValue, @IpAddress, @UserAgent, @CreatedAt
            );
            """,
            new
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TenantId = tenantId,
                Action = action,
                RoleId = roleId,
                NewValue = newValue,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                CreatedAt = createdAt
            },
            transaction,
            cancellationToken: cancellationToken));
    }

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

    private static IReadOnlyCollection<RoleDetailsDto> BuildRoles(IEnumerable<RolePermissionRow> rows)
    {
        return rows
            .GroupBy(row => row.Id)
            .Select(group =>
            {
                var first = group.First();
                return new RoleDetailsDto(
                    first.Id,
                    first.TenantId,
                    first.Name,
                    first.NormalizedName,
                    first.Description,
                    first.Scope,
                    first.IsSystemRole,
                    first.IsActive,
                    group
                        .Select(row => row.Permission)
                        .Where(permission => !string.IsNullOrWhiteSpace(permission))
                        .Select(permission => permission!)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(permission => permission)
                        .ToArray(),
                    first.CreatedAt,
                    first.UpdatedAt);
            })
            .ToArray();
    }

    private sealed class RolePermissionRow
    {
        public Guid Id { get; init; }
        public Guid? TenantId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string NormalizedName { get; init; } = string.Empty;
        public string? Description { get; init; }
        public string Scope { get; init; } = string.Empty;
        public bool IsSystemRole { get; init; }
        public bool IsActive { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; init; }
        public string? Permission { get; init; }
    }
}
