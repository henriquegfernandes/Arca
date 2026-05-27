namespace Arca.Application.Users;

public sealed record PermissionDto(
    Guid Id,
    string Name,
    string Description,
    string Module);

public sealed record RoleDetailsDto(
    Guid Id,
    Guid? TenantId,
    string Name,
    string NormalizedName,
    string? Description,
    string Scope,
    bool IsSystemRole,
    bool IsActive,
    IReadOnlyCollection<string> Permissions,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed class CreateRoleCommand
{
    public Guid? TenantId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Scope { get; init; } = "Store";
    public List<string> Permissions { get; init; } = [];
    public Guid? RequestedByUserId { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
}

public sealed class UpdateRolePermissionsCommand
{
    public Guid RoleId { get; init; }
    public List<string> Permissions { get; init; } = [];
    public Guid? RequestedByUserId { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
}

public sealed record CreateRoleData(
    Guid? TenantId,
    string Name,
    string NormalizedName,
    string? Description,
    string Scope,
    IReadOnlyCollection<string> Permissions,
    Guid? RequestedByUserId,
    string? IpAddress,
    string? UserAgent);

public sealed record UpdateRolePermissionsData(
    Guid RoleId,
    Guid? TenantId,
    IReadOnlyCollection<string> Permissions,
    Guid? RequestedByUserId,
    string? IpAddress,
    string? UserAgent);
