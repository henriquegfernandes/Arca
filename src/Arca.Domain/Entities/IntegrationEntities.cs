namespace Arca.Domain.Entities;

public sealed class ApiClient : BaseEntity
{
    public Guid TenantId { get; private set; }
    public Guid? StoreId { get; private set; }
    public string Name { get; private set; }
    public string ApiKeyHash { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime? LastUsedAt { get; private set; }

    public ApiClient(Guid tenantId, Guid? storeId, string name, string apiKeyHash)
    {
        TenantId = tenantId;
        StoreId = storeId;
        Name = RequireText(name, nameof(name));
        ApiKeyHash = RequireText(apiKeyHash, nameof(apiKeyHash));
        IsActive = true;
    }

    public void MarkUsed(DateTime when)
    {
        LastUsedAt = when;
        MarkUpdated();
    }

    public void Disable()
    {
        IsActive = false;
        MarkUpdated();
    }

    private static string RequireText(string value, string name) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{name} is required.", name) : value.Trim();
}

public sealed class AuditLog
{
    public Guid Id { get; private set; }
    public Guid? UserId { get; private set; }
    public Guid? TenantId { get; private set; }
    public Guid? StoreId { get; private set; }
    public string Action { get; private set; }
    public string EntityName { get; private set; }
    public Guid? EntityId { get; private set; }
    public string? OldValue { get; private set; }
    public string? NewValue { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public AuditLog(string action, string entityName, Guid? entityId, Guid? userId = null, Guid? tenantId = null, Guid? storeId = null)
    {
        Id = Guid.NewGuid();
        Action = RequireText(action, nameof(action));
        EntityName = RequireText(entityName, nameof(entityName));
        EntityId = entityId;
        UserId = userId;
        TenantId = tenantId;
        StoreId = storeId;
        CreatedAt = DateTime.UtcNow;
    }

    private static string RequireText(string value, string name) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{name} is required.", name) : value.Trim();
}

public sealed class LoginAttempt
{
    public Guid Id { get; private set; }
    public string Email { get; private set; }
    public bool Success { get; private set; }
    public string? FailureReason { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public LoginAttempt(string email, bool success, string? failureReason = null)
    {
        Id = Guid.NewGuid();
        Email = RequireText(email, nameof(email));
        Success = success;
        FailureReason = failureReason;
        CreatedAt = DateTime.UtcNow;
    }

    private static string RequireText(string value, string name) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{name} is required.", name) : value.Trim();
}
