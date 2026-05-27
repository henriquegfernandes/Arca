namespace Arca.Application.ExternalApi;

public static class ExternalApiPermissions
{
    public const string CatalogRead = "catalog.read";
    public const string InventoryRead = "inventory.read";
    public const string OrdersWrite = "orders.write";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        CatalogRead,
        InventoryRead,
        OrdersWrite
    };
}

public sealed class CreateApiClientCommand
{
    public Guid TenantId { get; init; }
    public Guid? StoreId { get; init; }
    public string Name { get; init; } = string.Empty;
    public List<string> Permissions { get; init; } = [];
}

public sealed record CreateApiClientResult(
    Guid Id,
    Guid TenantId,
    Guid? StoreId,
    string Name,
    string ApiKey,
    IReadOnlyCollection<string> Permissions);

public sealed record ApiClientDto(
    Guid Id,
    Guid TenantId,
    Guid? StoreId,
    string Name,
    bool IsActive,
    IReadOnlyCollection<string> Permissions,
    DateTime CreatedAt,
    DateTime? LastUsedAt);

public sealed record ExternalApiClientContext(
    Guid Id,
    Guid TenantId,
    Guid? StoreId,
    string Name,
    IReadOnlySet<string> Permissions)
{
    public bool HasPermission(string permission) => Permissions.Contains(permission);
}

public sealed record ExternalApiRequestLogData(
    Guid? ApiClientId,
    Guid? TenantId,
    Guid? StoreId,
    string Path,
    string Method,
    int StatusCode,
    string? IpAddress,
    string? UserAgent);
