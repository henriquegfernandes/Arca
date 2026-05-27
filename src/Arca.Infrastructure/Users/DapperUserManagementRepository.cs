using Arca.Application.Abstractions.Users;
using Arca.Application.Common;
using Arca.Application.Users;
using Arca.Infrastructure.Database;
using Dapper;

namespace Arca.Infrastructure.Users;

public sealed class DapperUserManagementRepository(IDbConnectionFactory connectionFactory) : IUserManagementRepository
{
    public async Task<PagedResult<UserSummaryDto>> ListUsersAsync(
        Guid? tenantId,
        PageRequest pageRequest,
        CancellationToken cancellationToken = default)
    {
        var paging = NormalizePaging(pageRequest);
        using var connection = connectionFactory.CreateConnection();
        using var result = await connection.QueryMultipleAsync(new CommandDefinition(
            """
            WITH filtered_users AS (
                SELECT DISTINCT u.id, u.created_at
                FROM app_user u
                LEFT JOIN user_role ur ON ur.user_id = u.id
                LEFT JOIN role r ON r.id = ur.role_id
                WHERE (
                        @TenantId IS NULL
                     OR ur.tenant_id = @TenantId
                     OR EXISTS (
                            SELECT 1
                            FROM user_tenant ut
                            WHERE ut.user_id = u.id
                              AND ut.tenant_id = @TenantId
                              AND ut.is_active = TRUE
                     )
                )
                  AND (
                        @Search IS NULL
                     OR u.full_name ILIKE @Search
                     OR u.email ILIKE @Search
                     OR u.phone ILIKE @Search
                     OR r.name ILIKE @Search
                  )
            ),
            selected_users AS (
                SELECT id
                FROM filtered_users
                ORDER BY created_at DESC
                LIMIT @PageSize OFFSET @Offset
            )
            SELECT COUNT(*)::int FROM filtered_users;

            WITH filtered_users AS (
                SELECT DISTINCT u.id, u.created_at
                FROM app_user u
                LEFT JOIN user_role ur ON ur.user_id = u.id
                LEFT JOIN role r ON r.id = ur.role_id
                WHERE (
                        @TenantId IS NULL
                     OR ur.tenant_id = @TenantId
                     OR EXISTS (
                            SELECT 1
                            FROM user_tenant ut
                            WHERE ut.user_id = u.id
                              AND ut.tenant_id = @TenantId
                              AND ut.is_active = TRUE
                     )
                )
                  AND (
                        @Search IS NULL
                     OR u.full_name ILIKE @Search
                     OR u.email ILIKE @Search
                     OR u.phone ILIKE @Search
                     OR r.name ILIKE @Search
                  )
            ),
            selected_users AS (
                SELECT id
                FROM filtered_users
                ORDER BY created_at DESC
                LIMIT @PageSize OFFSET @Offset
            )
            SELECT
                u.id AS Id,
                u.full_name AS FullName,
                u.email AS Email,
                u.phone AS Phone,
                u.is_active AS IsActive,
                u.email_confirmed AS EmailConfirmed,
                u.last_login_at AS LastLoginAt,
                u.created_at AS CreatedAt,
                r.id AS RoleId,
                r.name AS RoleName,
                r.scope AS Scope,
                ur.tenant_id AS TenantId,
                ur.store_id AS StoreId
            FROM app_user u
            INNER JOIN selected_users su ON su.id = u.id
            LEFT JOIN user_role ur ON ur.user_id = u.id
            LEFT JOIN role r ON r.id = ur.role_id
            ORDER BY u.created_at DESC, r.name;
            """,
            new { TenantId = tenantId, paging.Search, paging.PageSize, paging.Offset },
            cancellationToken: cancellationToken));

        var totalCount = await result.ReadSingleAsync<int>();
        var rows = (await result.ReadAsync<UserListRow>()).ToArray();
        var users = rows
            .GroupBy(row => row.Id)
            .Select(group =>
            {
                var first = group.First();
                return new UserSummaryDto(
                    first.Id,
                    first.FullName,
                    first.Email,
                    first.Phone,
                    first.IsActive,
                    first.EmailConfirmed,
                    first.LastLoginAt,
                    first.CreatedAt,
                    group
                        .Where(row => row.RoleId is not null)
                        .Select(row => new UserRoleAssignmentDto(
                            row.RoleId!.Value,
                            row.RoleName ?? string.Empty,
                            row.Scope ?? string.Empty,
                            row.TenantId,
                            row.StoreId))
                        .ToArray());
            })
            .ToArray();

        return new PagedResult<UserSummaryDto>(users, totalCount, paging.Page, paging.PageSize);
    }

    public async Task<PagedResult<RoleSummaryDto>> ListRolesAsync(
        Guid? tenantId,
        PageRequest pageRequest,
        CancellationToken cancellationToken = default)
    {
        var paging = NormalizePaging(pageRequest);
        using var connection = connectionFactory.CreateConnection();
        using var result = await connection.QueryMultipleAsync(new CommandDefinition(
            """
            SELECT COUNT(*)::int
            FROM role
            WHERE is_active = TRUE
              AND (
                    tenant_id IS NULL
                 OR (@TenantId IS NOT NULL AND tenant_id = @TenantId)
              )
              AND (
                    @Search IS NULL
                 OR name ILIKE @Search
                 OR scope ILIKE @Search
              );

            SELECT
                id AS Id,
                tenant_id AS TenantId,
                name AS Name,
                scope AS Scope,
                is_system_role AS IsSystemRole,
                is_active AS IsActive
            FROM role
            WHERE is_active = TRUE
              AND (
                    tenant_id IS NULL
                 OR (@TenantId IS NOT NULL AND tenant_id = @TenantId)
              )
              AND (
                    @Search IS NULL
                 OR name ILIKE @Search
                 OR scope ILIKE @Search
              )
            ORDER BY scope, name
            LIMIT @PageSize OFFSET @Offset;
            """,
            new { TenantId = tenantId, paging.Search, paging.PageSize, paging.Offset },
            cancellationToken: cancellationToken));

        var totalCount = await result.ReadSingleAsync<int>();
        var roles = (await result.ReadAsync<RoleSummaryDto>()).ToArray();
        return new PagedResult<RoleSummaryDto>(roles, totalCount, paging.Page, paging.PageSize);
    }

    public async Task<bool> UserEmailExistsAsync(
        string normalizedEmail,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM app_user WHERE normalized_email = @NormalizedEmail);",
            new { NormalizedEmail = normalizedEmail },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> TenantExistsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM tenant WHERE id = @TenantId AND is_active = TRUE);",
            new { TenantId = tenantId },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> StoreBelongsToTenantAsync(
        Guid tenantId,
        Guid storeId,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM store WHERE id = @StoreId AND tenant_id = @TenantId AND is_active = TRUE);",
            new { TenantId = tenantId, StoreId = storeId },
            cancellationToken: cancellationToken));
    }

    public async Task<RoleSummaryDto?> GetRoleAsync(
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<RoleSummaryDto>(new CommandDefinition(
            """
            SELECT
                id AS Id,
                tenant_id AS TenantId,
                name AS Name,
                scope AS Scope,
                is_system_role AS IsSystemRole,
                is_active AS IsActive
            FROM role
            WHERE id = @RoleId;
            """,
            new { RoleId = roleId },
            cancellationToken: cancellationToken));
    }

    public async Task<UserSummaryDto> CreateUserAsync(
        CreateUserData data,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            var now = DateTime.UtcNow;
            var userId = Guid.NewGuid();

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO app_user (
                    id, full_name, email, normalized_email, phone, password_hash,
                    is_active, email_confirmed, last_login_at, created_at, updated_at
                )
                VALUES (
                    @Id, @FullName, @Email, @NormalizedEmail, @Phone, @PasswordHash,
                    TRUE, TRUE, NULL, @CreatedAt, NULL
                );
                """,
                new
                {
                    Id = userId,
                    data.FullName,
                    data.Email,
                    data.NormalizedEmail,
                    data.Phone,
                    data.PasswordHash,
                    CreatedAt = now
                },
                transaction,
                cancellationToken: cancellationToken));

            if (data.TenantId is not null)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO user_tenant (id, user_id, tenant_id, is_active, created_at, updated_at)
                    VALUES (@Id, @UserId, @TenantId, TRUE, @CreatedAt, NULL)
                    ON CONFLICT (user_id, tenant_id)
                    DO UPDATE SET is_active = TRUE, updated_at = EXCLUDED.created_at;
                    """,
                    new { Id = Guid.NewGuid(), UserId = userId, TenantId = data.TenantId.Value, CreatedAt = now },
                    transaction,
                    cancellationToken: cancellationToken));
            }

            if (data.TenantId is not null && data.StoreId is not null)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO user_store (id, user_id, tenant_id, store_id, is_active, created_at, updated_at)
                    VALUES (@Id, @UserId, @TenantId, @StoreId, TRUE, @CreatedAt, NULL)
                    ON CONFLICT (user_id, store_id)
                    DO UPDATE SET is_active = TRUE, updated_at = EXCLUDED.created_at;
                    """,
                    new
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        TenantId = data.TenantId.Value,
                        StoreId = data.StoreId.Value,
                        CreatedAt = now
                    },
                    transaction,
                    cancellationToken: cancellationToken));
            }

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO user_role (id, user_id, role_id, tenant_id, store_id, created_at)
                VALUES (@Id, @UserId, @RoleId, @TenantId, @StoreId, @CreatedAt);
                """,
                new
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    data.RoleId,
                    data.TenantId,
                    data.StoreId,
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
                    @Id, @RequestedByUserId, @TenantId, @StoreId, 'users.create', 'User', @UserId,
                    NULL, @NewValue, @IpAddress, @UserAgent, @CreatedAt
                );
                """,
                new
                {
                    Id = Guid.NewGuid(),
                    data.RequestedByUserId,
                    data.TenantId,
                    data.StoreId,
                    UserId = userId,
                    NewValue = $"Email={data.Email}; RoleId={data.RoleId}",
                    data.IpAddress,
                    data.UserAgent,
                    CreatedAt = now
                },
                transaction,
                cancellationToken: cancellationToken));

            transaction.Commit();

            return new UserSummaryDto(
                userId,
                data.FullName,
                data.Email,
                data.Phone,
                true,
                true,
                null,
                now,
                [new UserRoleAssignmentDto(data.RoleId, string.Empty, data.RoleScope, data.TenantId, data.StoreId)]);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<bool> DisableUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE app_user
            SET is_active = FALSE,
                updated_at = @UpdatedAt
            WHERE id = @UserId
              AND is_active = TRUE;
            """,
            new { UserId = userId, UpdatedAt = DateTime.UtcNow },
            cancellationToken: cancellationToken));

        return affected > 0;
    }

    private sealed class UserListRow
    {
        public Guid Id { get; init; }
        public string FullName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string? Phone { get; init; }
        public bool IsActive { get; init; }
        public bool EmailConfirmed { get; init; }
        public DateTime? LastLoginAt { get; init; }
        public DateTime CreatedAt { get; init; }
        public Guid? RoleId { get; init; }
        public string? RoleName { get; init; }
        public string? Scope { get; init; }
        public Guid? TenantId { get; init; }
        public Guid? StoreId { get; init; }
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
}
