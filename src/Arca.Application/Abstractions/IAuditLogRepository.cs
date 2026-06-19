using Arca.Application.AuditLog;
using Arca.Application.Common;

namespace Arca.Application.Abstractions;

public interface IAuditLogRepository
{
    Task<PagedResult<AuditLogEntryDto>> ListAsync(
        Guid? tenantId,
        Guid? storeId,
        Guid? userId,
        string? entityName,
        string? action,
        DateTime? dateFrom,
        DateTime? dateTo,
        PageRequest pageRequest,
        CancellationToken cancellationToken = default);
}
