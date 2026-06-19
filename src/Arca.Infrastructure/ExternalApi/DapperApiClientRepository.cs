using Arca.Application.Abstractions.ExternalApi;
using Arca.Application.Common;
using Arca.Application.ExternalApi;
using Arca.Infrastructure.Database;
using Dapper;

namespace Arca.Infrastructure.ExternalApi;

public sealed class DapperApiClientRepository(IDbConnectionFactory connectionFactory) : IApiClientRepository
{
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

    public async Task<ApiClientDto> CreateAsync(
        CreateApiClientCommand command,
        string apiKeyHash,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            var now = DateTime.UtcNow;
            var apiClientId = Guid.NewGuid();

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO api_client (
                    id, tenant_id, store_id, name, api_key_hash,
                    is_active, created_at, updated_at, last_used_at
                )
                VALUES (
                    @Id, @TenantId, @StoreId, @Name, @ApiKeyHash,
                    TRUE, @CreatedAt, NULL, NULL
                );
                """,
                new
                {
                    Id = apiClientId,
                    command.TenantId,
                    command.StoreId,
                    command.Name,
                    ApiKeyHash = apiKeyHash,
                    CreatedAt = now
                },
                transaction,
                cancellationToken: cancellationToken));

            foreach (var permission in command.Permissions)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO api_client_permission (id, api_client_id, permission)
                    VALUES (@Id, @ApiClientId, @Permission)
                    ON CONFLICT (api_client_id, permission) DO NOTHING;
                    """,
                    new { Id = Guid.NewGuid(), ApiClientId = apiClientId, Permission = permission },
                    transaction,
                    cancellationToken: cancellationToken));
            }

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO audit_log (
                    id, user_id, tenant_id, store_id, action, entity_name, entity_id,
                    old_value, new_value, ip_address, user_agent, created_at
                )
                VALUES (
                    @Id, NULL, @TenantId, @StoreId, 'api_clients.create', 'ApiClient', @ApiClientId,
                    NULL, @NewValue, NULL, NULL, @CreatedAt
                );
                """,
                new
                {
                    Id = Guid.NewGuid(),
                    command.TenantId,
                    command.StoreId,
                    ApiClientId = apiClientId,
                    NewValue = $"Name={command.Name}; Permissions={string.Join(",", command.Permissions)}",
                    CreatedAt = now
                },
                transaction,
                cancellationToken: cancellationToken));

            transaction.Commit();

            return new ApiClientDto(
                apiClientId,
                command.TenantId,
                command.StoreId,
                command.Name,
                true,
                command.Permissions,
                now,
                null);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<PagedResult<ApiClientDto>> ListAsync(
        Guid tenantId,
        PageRequest pageRequest,
        CancellationToken cancellationToken = default)
    {
        var paging = NormalizePaging(pageRequest);
        using var connection = connectionFactory.CreateConnection();
        using var result = await connection.QueryMultipleAsync(new CommandDefinition(
            """
            WITH filtered_clients AS (
                SELECT DISTINCT ac.id, ac.created_at
                FROM api_client ac
                LEFT JOIN api_client_permission acp ON acp.api_client_id = ac.id
                WHERE ac.tenant_id = @TenantId
                  AND (
                        @Search IS NULL
                     OR ac.name ILIKE @Search
                     OR acp.permission ILIKE @Search
                  )
            )
            SELECT COUNT(*)::int FROM filtered_clients;

            WITH filtered_clients AS (
                SELECT DISTINCT ac.id, ac.created_at
                FROM api_client ac
                LEFT JOIN api_client_permission acp ON acp.api_client_id = ac.id
                WHERE ac.tenant_id = @TenantId
                  AND (
                        @Search IS NULL
                     OR ac.name ILIKE @Search
                     OR acp.permission ILIKE @Search
                  )
            ),
            selected_clients AS (
                SELECT id
                FROM filtered_clients
                ORDER BY created_at DESC
                LIMIT @PageSize OFFSET @Offset
            )
            SELECT
                ac.id AS Id,
                ac.tenant_id AS TenantId,
                ac.store_id AS StoreId,
                ac.name AS Name,
                ac.is_active AS IsActive,
                ac.created_at AS CreatedAt,
                ac.last_used_at AS LastUsedAt,
                acp.permission AS Permission
            FROM api_client ac
            INNER JOIN selected_clients sc ON sc.id = ac.id
            LEFT JOIN api_client_permission acp ON acp.api_client_id = ac.id
            ORDER BY ac.created_at DESC, acp.permission;
            """,
            new { TenantId = tenantId, paging.Search, paging.PageSize, paging.Offset },
            cancellationToken: cancellationToken));

        var totalCount = await result.ReadSingleAsync<int>();
        var rows = (await result.ReadAsync<ApiClientRow>()).ToArray();
        var clients = rows
            .GroupBy(row => row.Id)
            .Select(group =>
            {
                var first = group.First();
                return new ApiClientDto(
                    first.Id,
                    first.TenantId,
                    first.StoreId,
                    first.Name,
                    first.IsActive,
                    group.Select(row => row.Permission)
                        .Where(permission => !string.IsNullOrWhiteSpace(permission))
                        .Select(permission => permission!)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray(),
                    first.CreatedAt,
                    first.LastUsedAt);
            })
            .ToArray();

        return new PagedResult<ApiClientDto>(clients, totalCount, paging.Page, paging.PageSize);
    }

    public async Task<bool> DisableAsync(
        Guid tenantId,
        Guid apiClientId,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE api_client
            SET is_active = FALSE,
                updated_at = @UpdatedAt
            WHERE id = @ApiClientId
              AND tenant_id = @TenantId
              AND is_active = TRUE;
            """,
            new { TenantId = tenantId, ApiClientId = apiClientId, UpdatedAt = DateTime.UtcNow },
            cancellationToken: cancellationToken));

        return affected > 0;
    }

    public async Task<bool> DeleteAsync(
        DeleteApiClientCommand command,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            var existing = await connection.QuerySingleOrDefaultAsync<ApiClientExistingRow>(new CommandDefinition(
                """
                SELECT
                    id AS Id,
                    tenant_id AS TenantId,
                    store_id AS StoreId,
                    name AS Name,
                    is_active AS IsActive,
                    created_at AS CreatedAt,
                    last_used_at AS LastUsedAt
                FROM api_client
                WHERE id = @ApiClientId
                  AND tenant_id = @TenantId;
                """,
                new { command.ApiClientId, command.TenantId },
                transaction,
                cancellationToken: cancellationToken));

            if (existing is null)
            {
                transaction.Rollback();
                return false;
            }

            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE external_api_request_log SET api_client_id = NULL WHERE api_client_id = @ApiClientId;",
                new { command.ApiClientId },
                transaction,
                cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition(
                "DELETE FROM api_client_permission WHERE api_client_id = @ApiClientId;",
                new { command.ApiClientId },
                transaction,
                cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition(
                """
                DELETE FROM api_client
                WHERE id = @ApiClientId
                  AND tenant_id = @TenantId;
                """,
                new { command.ApiClientId, command.TenantId },
                transaction,
                cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO audit_log (
                    id, user_id, tenant_id, store_id, action, entity_name, entity_id,
                    old_value, new_value, ip_address, user_agent, created_at
                )
                VALUES (
                    @Id, @RequestedByUserId, @TenantId, @StoreId, 'api_clients.delete', 'ApiClient', @ApiClientId,
                    @OldValue, NULL, @IpAddress, @UserAgent, @CreatedAt
                );
                """,
                new
                {
                    Id = Guid.NewGuid(),
                    command.RequestedByUserId,
                    command.TenantId,
                    existing.StoreId,
                    command.ApiClientId,
                    OldValue = $"Name={existing.Name}; StoreId={existing.StoreId}; IsActive={existing.IsActive}",
                    command.IpAddress,
                    command.UserAgent,
                    CreatedAt = DateTime.UtcNow
                },
                transaction,
                cancellationToken: cancellationToken));

            transaction.Commit();
            return true;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<ApiClientDto?> UpdateAsync(
        UpdateApiClientCommand command,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            var existing = await connection.QuerySingleOrDefaultAsync<ApiClientExistingRow>(new CommandDefinition(
                """
                SELECT
                    ac.id AS Id,
                    ac.tenant_id AS TenantId,
                    ac.store_id AS StoreId,
                    ac.name AS Name,
                    ac.is_active AS IsActive,
                    ac.created_at AS CreatedAt,
                    ac.last_used_at AS LastUsedAt
                FROM api_client ac
                WHERE ac.id = @ApiClientId
                  AND ac.tenant_id = @TenantId;
                """,
                new { command.ApiClientId, command.TenantId },
                transaction,
                cancellationToken: cancellationToken));

            if (existing is null)
            {
                transaction.Rollback();
                return null;
            }

            var existingPermissions = (await connection.QueryAsync<string>(new CommandDefinition(
                """
                SELECT permission
                FROM api_client_permission
                WHERE api_client_id = @ApiClientId
                ORDER BY permission;
                """,
                new { command.ApiClientId },
                transaction,
                cancellationToken: cancellationToken))).ToArray();

            var now = DateTime.UtcNow;
            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE api_client
                SET name = @Name,
                    store_id = @StoreId,
                    is_active = @IsActive,
                    updated_at = @UpdatedAt
                WHERE id = @ApiClientId
                  AND tenant_id = @TenantId;
                """,
                new
                {
                    command.ApiClientId,
                    command.TenantId,
                    command.StoreId,
                    command.Name,
                    command.IsActive,
                    UpdatedAt = now
                },
                transaction,
                cancellationToken: cancellationToken));

            await connection.ExecuteAsync(new CommandDefinition(
                "DELETE FROM api_client_permission WHERE api_client_id = @ApiClientId;",
                new { command.ApiClientId },
                transaction,
                cancellationToken: cancellationToken));

            foreach (var permission in command.Permissions)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO api_client_permission (id, api_client_id, permission)
                    VALUES (@Id, @ApiClientId, @Permission)
                    ON CONFLICT (api_client_id, permission) DO NOTHING;
                    """,
                    new { Id = Guid.NewGuid(), command.ApiClientId, Permission = permission },
                    transaction,
                    cancellationToken: cancellationToken));
            }

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO audit_log (
                    id, user_id, tenant_id, store_id, action, entity_name, entity_id,
                    old_value, new_value, ip_address, user_agent, created_at
                )
                VALUES (
                    @Id, @RequestedByUserId, @TenantId, @StoreId, 'api_clients.update', 'ApiClient', @ApiClientId,
                    @OldValue, @NewValue, @IpAddress, @UserAgent, @CreatedAt
                );
                """,
                new
                {
                    Id = Guid.NewGuid(),
                    command.RequestedByUserId,
                    command.TenantId,
                    command.StoreId,
                    command.ApiClientId,
                    OldValue = $"Name={existing.Name}; StoreId={existing.StoreId}; IsActive={existing.IsActive}; Permissions={string.Join(",", existingPermissions)}",
                    NewValue = $"Name={command.Name}; StoreId={command.StoreId}; IsActive={command.IsActive}; Permissions={string.Join(",", command.Permissions)}",
                    command.IpAddress,
                    command.UserAgent,
                    CreatedAt = now
                },
                transaction,
                cancellationToken: cancellationToken));

            transaction.Commit();

            return new ApiClientDto(
                existing.Id,
                existing.TenantId,
                command.StoreId,
                command.Name,
                command.IsActive,
                command.Permissions,
                existing.CreatedAt,
                existing.LastUsedAt);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<ExternalApiClientContext?> AuthenticateAsync(
        string apiKeyHash,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        using var result = await connection.QueryMultipleAsync(new CommandDefinition(
            """
            SELECT
                id AS Id,
                tenant_id AS TenantId,
                store_id AS StoreId,
                name AS Name
            FROM api_client
            WHERE api_key_hash = @ApiKeyHash
              AND is_active = TRUE;

            SELECT acp.permission
            FROM api_client ac
            INNER JOIN api_client_permission acp ON acp.api_client_id = ac.id
            WHERE ac.api_key_hash = @ApiKeyHash
              AND ac.is_active = TRUE;
            """,
            new { ApiKeyHash = apiKeyHash },
            cancellationToken: cancellationToken));

        var client = await result.ReadSingleOrDefaultAsync<ApiClientAuthRecord>();
        if (client is null)
        {
            return null;
        }

        var permissions = (await result.ReadAsync<string>()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return new ExternalApiClientContext(client.Id, client.TenantId, client.StoreId, client.Name, permissions);
    }

    public async Task TouchLastUsedAsync(Guid apiClientId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE api_client SET last_used_at = @LastUsedAt WHERE id = @ApiClientId;",
            new { ApiClientId = apiClientId, LastUsedAt = DateTime.UtcNow },
            cancellationToken: cancellationToken));
    }

    public async Task LogRequestAsync(
        ExternalApiRequestLogData requestLog,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO external_api_request_log (
                id, api_client_id, tenant_id, store_id, path, method,
                status_code, ip_address, user_agent, created_at
            )
            VALUES (
                @Id, @ApiClientId, @TenantId, @StoreId, @Path, @Method,
                @StatusCode, @IpAddress, @UserAgent, @CreatedAt
            );
            """,
            new
            {
                Id = Guid.NewGuid(),
                requestLog.ApiClientId,
                requestLog.TenantId,
                requestLog.StoreId,
                requestLog.Path,
                requestLog.Method,
                requestLog.StatusCode,
                requestLog.IpAddress,
                requestLog.UserAgent,
                CreatedAt = DateTime.UtcNow
            },
            cancellationToken: cancellationToken));
    }

    private sealed class ApiClientRow
    {
        public Guid Id { get; init; }
        public Guid TenantId { get; init; }
        public Guid? StoreId { get; init; }
        public string Name { get; init; } = string.Empty;
        public bool IsActive { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? LastUsedAt { get; init; }
        public string? Permission { get; set; }
    }

    private sealed class ApiClientExistingRow
    {
        public Guid Id { get; init; }
        public Guid TenantId { get; init; }
        public Guid? StoreId { get; init; }
        public string Name { get; init; } = string.Empty;
        public bool IsActive { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? LastUsedAt { get; init; }
    }

    private sealed class ApiClientAuthRecord
    {
        public Guid Id { get; init; }
        public Guid TenantId { get; init; }
        public Guid? StoreId { get; init; }
        public string Name { get; init; } = string.Empty;
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
