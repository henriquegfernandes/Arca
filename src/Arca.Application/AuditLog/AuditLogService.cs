using Arca.Application.Abstractions;
using Arca.Application.Common;

namespace Arca.Application.AuditLog;

public sealed class AuditLogService(IAuditLogRepository repository)
{
    public async Task<Result<PagedResult<AuditLogEntryDto>>> ListAsync(
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
        var result = await repository.ListAsync(
            tenantId, storeId, userId, entityName, action, dateFrom, dateTo, pageRequest, cancellationToken);

        return Result<PagedResult<AuditLogEntryDto>>.Success(result);
    }
}
