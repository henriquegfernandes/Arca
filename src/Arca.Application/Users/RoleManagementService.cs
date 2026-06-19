using Arca.Application.Abstractions.Users;
using Arca.Application.Common;

namespace Arca.Application.Users;

public sealed class RoleManagementService(IRoleManagementRepository repository)
{
    private static readonly HashSet<string> KnownScopes = new(StringComparer.OrdinalIgnoreCase)
    {
        "System",
        "Tenant",
        "Store"
    };

    public async Task<Result<IReadOnlyCollection<PermissionDto>>> ListPermissionsAsync(
        CancellationToken cancellationToken = default)
    {
        var permissions = await repository.ListPermissionsAsync(cancellationToken);
        return Result<IReadOnlyCollection<PermissionDto>>.Success(permissions);
    }

    public async Task<Result<PagedResult<RoleDetailsDto>>> ListRolesAsync(
        Guid? tenantId,
        PageRequest pageRequest,
        CancellationToken cancellationToken = default)
    {
        var roles = await repository.ListRolesAsync(tenantId, pageRequest, cancellationToken);
        return Result<PagedResult<RoleDetailsDto>>.Success(roles);
    }

    public async Task<Result<RoleDetailsDto>> CreateRoleAsync(
        CreateRoleCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationError = await ValidateCreateAsync(command, cancellationToken);
        if (validationError is not null)
        {
            return Result<RoleDetailsDto>.Failure(validationError);
        }

        var permissions = NormalizePermissions(command.Permissions);
        var role = await repository.CreateRoleAsync(new CreateRoleData(
            NormalizeTenantId(command.Scope, command.TenantId),
            command.Name.Trim(),
            NormalizeName(command.Name),
            TrimToNull(command.Description),
            NormalizeScope(command.Scope),
            permissions,
            command.RequestedByUserId,
            command.IpAddress,
            command.UserAgent), cancellationToken);

        return Result<RoleDetailsDto>.Success(role);
    }

    public async Task<Result<RoleDetailsDto>> UpdateRolePermissionsAsync(
        UpdateRolePermissionsCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.RoleId == Guid.Empty)
        {
            return Result<RoleDetailsDto>.Failure("RoleId is required.");
        }

        var role = await repository.GetRoleAsync(command.RoleId, cancellationToken);
        if (role is null)
        {
            return Result<RoleDetailsDto>.Failure("Role was not found.");
        }

        var validationError = await ValidatePermissionsAsync(command.Permissions, cancellationToken);
        if (validationError is not null)
        {
            return Result<RoleDetailsDto>.Failure(validationError);
        }

        var updated = await repository.UpdateRolePermissionsAsync(new UpdateRolePermissionsData(
            command.RoleId,
            role.TenantId,
            NormalizePermissions(command.Permissions),
            command.RequestedByUserId,
            command.IpAddress,
            command.UserAgent), cancellationToken);

        return updated is null
            ? Result<RoleDetailsDto>.Failure("Role was not found.")
            : Result<RoleDetailsDto>.Success(updated);
    }

    public async Task<Result> DisableRoleAsync(
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        if (roleId == Guid.Empty)
        {
            return Result.Failure("RoleId is required.");
        }

        var role = await repository.GetRoleAsync(roleId, cancellationToken);
        if (role is null)
        {
            return Result.Failure("Role was not found.");
        }

        if (role.IsSystemRole)
        {
            return Result.Failure("System roles cannot be disabled.");
        }

        var disabled = await repository.DisableRoleAsync(roleId, cancellationToken);
        return disabled ? Result.Success() : Result.Failure("Role was not found.");
    }

    public async Task<Result> DeleteRoleAsync(
        UpdateRolePermissionsCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.RoleId == Guid.Empty)
        {
            return Result.Failure("RoleId is required.");
        }

        var role = await repository.GetRoleAsync(command.RoleId, cancellationToken);
        if (role is null)
        {
            return Result.Failure("Role was not found.");
        }

        if (role.IsSystemRole || role.Scope.Equals("System", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure("System roles cannot be deleted.");
        }

        var deleted = await repository.DeleteRoleAsync(
            new DeleteRoleData(
                role.Id,
                role.TenantId,
                role.Name,
                command.RequestedByUserId,
                command.IpAddress,
                command.UserAgent),
            cancellationToken);

        return deleted ? Result.Success() : Result.Failure("Role was not found.");
    }

    public async Task<Result> ActivateRoleAsync(
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        if (roleId == Guid.Empty)
        {
            return Result.Failure("RoleId is required.");
        }

        var role = await repository.GetRoleAsync(roleId, cancellationToken);
        if (role is null)
        {
            return Result.Failure("Role was not found.");
        }

        if (role.IsSystemRole)
        {
            return Result.Failure("System roles are always managed by platform seed.");
        }

        var activated = await repository.ActivateRoleAsync(roleId, cancellationToken);
        return activated ? Result.Success() : Result.Failure("Role was not found.");
    }

    private async Task<string?> ValidateCreateAsync(
        CreateRoleCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return "Role name is required.";
        }

        if (!KnownScopes.Contains(command.Scope))
        {
            return "Role scope is invalid.";
        }

        var scope = NormalizeScope(command.Scope);
        if (scope == "System" && command.TenantId is not null)
        {
            return "System roles cannot be scoped to a tenant.";
        }

        if ((scope == "Tenant" || scope == "Store") && command.TenantId is null)
        {
            return "TenantId is required for Tenant and Store roles.";
        }

        if (command.TenantId is not null
            && !await repository.TenantExistsAsync(command.TenantId.Value, cancellationToken))
        {
            return "Tenant was not found.";
        }

        if (await repository.RoleNameExistsAsync(NormalizeTenantId(scope, command.TenantId), NormalizeName(command.Name), null, cancellationToken))
        {
            return "Role name is already in use for this scope.";
        }

        return await ValidatePermissionsAsync(command.Permissions, cancellationToken);
    }

    private async Task<string?> ValidatePermissionsAsync(
        IReadOnlyCollection<string> permissions,
        CancellationToken cancellationToken)
    {
        if (permissions.Count == 0)
        {
            return "At least one permission is required.";
        }

        var knownPermissions = (await repository.ListPermissionsAsync(cancellationToken))
            .Select(permission => permission.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var invalidPermission = permissions.FirstOrDefault(permission => !knownPermissions.Contains(permission.Trim()));
        return invalidPermission is null ? null : $"Invalid permission: {invalidPermission}.";
    }

    private static IReadOnlyCollection<string> NormalizePermissions(IEnumerable<string> permissions) =>
        permissions
            .Select(permission => permission.Trim().ToLowerInvariant())
            .Where(permission => permission.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static Guid? NormalizeTenantId(string scope, Guid? tenantId) =>
        NormalizeScope(scope) == "System" ? null : tenantId;

    private static string NormalizeScope(string scope) =>
        char.ToUpperInvariant(scope.Trim()[0]) + scope.Trim()[1..].ToLowerInvariant();

    private static string NormalizeName(string name) => name.Trim().ToUpperInvariant();

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
