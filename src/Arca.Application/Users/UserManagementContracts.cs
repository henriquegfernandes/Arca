namespace Arca.Application.Users;

public sealed record UserSummaryDto(
    Guid Id,
    string FullName,
    string Email,
    string? Phone,
    bool IsActive,
    bool EmailConfirmed,
    DateTime? LastLoginAt,
    DateTime CreatedAt,
    IReadOnlyCollection<UserRoleAssignmentDto> Roles);

public sealed record UserRoleAssignmentDto(
    Guid RoleId,
    string RoleName,
    string Scope,
    Guid? TenantId,
    Guid? StoreId);

public sealed record RoleSummaryDto(
    Guid Id,
    Guid? TenantId,
    string Name,
    string Scope,
    bool IsSystemRole,
    bool IsActive);

public sealed class CreateUserCommand
{
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string TemporaryPassword { get; init; } = string.Empty;
    public Guid RoleId { get; init; }
    public Guid? TenantId { get; init; }
    public Guid? StoreId { get; init; }
    public Guid? RequestedByUserId { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
}

public sealed record CreateUserData(
    string FullName,
    string Email,
    string NormalizedEmail,
    string? Phone,
    string PasswordHash,
    Guid RoleId,
    string RoleScope,
    Guid? TenantId,
    Guid? StoreId,
    Guid? RequestedByUserId,
    string? IpAddress,
    string? UserAgent);
