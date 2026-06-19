namespace Arca.Application.AuditLog;

public sealed record AuditLogEntryDto(
    Guid Id,
    Guid? UserId,
    Guid? TenantId,
    Guid? StoreId,
    string Action,
    string EntityName,
    Guid? EntityId,
    string? OldValue,
    string? NewValue,
    string? IpAddress,
    string? UserAgent,
    DateTime CreatedAt);
