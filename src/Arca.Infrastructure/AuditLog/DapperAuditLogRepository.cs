using Arca.Application.Abstractions;
using Arca.Application.AuditLog;
using Arca.Application.Common;
using Arca.Infrastructure.Database;
using Dapper;

namespace Arca.Infrastructure.AuditLog;

public sealed class DapperAuditLogRepository(IDbConnectionFactory connectionFactory) : IAuditLogRepository
{
    public async Task<PagedResult<AuditLogEntryDto>> ListAsync(
        Guid? tenantId,
        Guid? storeId,
        Guid? userId,
        string? entityName,
        string? action,
        DateTime? dateFrom,
        DateTime? dateTo,
        PageRequest pageRequest,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        var paging = NormalizePaging(pageRequest);

        var where = BuildWhereClause(tenantId, storeId, userId, entityName, action, dateFrom, dateTo, paging.Search);

        var items = await connection.QueryAsync<AuditLogEntryDto>(new CommandDefinition(
            $"""
            SELECT id, user_id AS UserId, tenant_id AS TenantId, store_id AS StoreId,
                   action, entity_name AS EntityName, entity_id AS EntityId,
                   old_value AS OldValue, new_value AS NewValue,
                   ip_address AS IpAddress, user_agent AS UserAgent, created_at AS CreatedAt
            FROM audit_log
            {where}
            ORDER BY created_at DESC
            LIMIT @PageSize OFFSET @Offset;
            """,
            new
            {
                TenantId = tenantId,
                StoreId = storeId,
                UserId = userId,
                EntityName = entityName is null ? null : $"%{EscapeLike(entityName)}%",
                Action = action is null ? null : $"%{EscapeLike(action)}%",
                DateFrom = dateFrom,
                DateTo = dateTo,
                Search = paging.Search,
                paging.PageSize,
                paging.Offset
            },
            cancellationToken: cancellationToken));

        var totalCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(*)::int FROM audit_log {where};",
            new
            {
                TenantId = tenantId,
                StoreId = storeId,
                UserId = userId,
                EntityName = entityName is null ? null : $"%{EscapeLike(entityName)}%",
                Action = action is null ? null : $"%{EscapeLike(action)}%",
                DateFrom = dateFrom,
                DateTo = dateTo,
                Search = paging.Search
            },
            cancellationToken: cancellationToken));

        return new PagedResult<AuditLogEntryDto>(items.ToArray(), totalCount, paging.Page, paging.PageSize);
    }

    private static string BuildWhereClause(
        Guid? tenantId, Guid? storeId, Guid? userId, string? entityName, string? action,
        DateTime? dateFrom, DateTime? dateTo, string? search)
    {
        var conditions = new List<string>();

        if (tenantId.HasValue)
            conditions.Add("tenant_id = @TenantId");
        if (storeId.HasValue)
            conditions.Add("store_id = @StoreId");
        if (userId.HasValue)
            conditions.Add("user_id = @UserId");
        if (entityName is not null)
            conditions.Add("entity_name ILIKE @EntityName");
        if (action is not null)
            conditions.Add("action ILIKE @Action");
        if (dateFrom.HasValue)
            conditions.Add("created_at >= @DateFrom");
        if (dateTo.HasValue)
            conditions.Add("created_at <= @DateTo");
        if (search is not null)
        {
            conditions.Add(
                "(action ILIKE @Search OR entity_name ILIKE @Search " +
                "OR entity_id::text ILIKE @Search OR old_value ILIKE @Search " +
                "OR new_value ILIKE @Search)");
        }

        return conditions.Count > 0
            ? $"WHERE {string.Join(" AND ", conditions)}"
            : "";
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
