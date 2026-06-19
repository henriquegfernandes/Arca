using Arca.Application.Abstractions.Users;
using Arca.Application.Common;
using Arca.Application.Security;

namespace Arca.Application.Users;

public sealed class UserManagementService(
    IUserManagementRepository repository,
    IPasswordHasher passwordHasher)
{
    public async Task<Result<PagedResult<UserSummaryDto>>> ListUsersAsync(
        Guid? tenantId,
        PageRequest pageRequest,
        CancellationToken cancellationToken = default)
    {
        var users = await repository.ListUsersAsync(tenantId, pageRequest, cancellationToken);
        return Result<PagedResult<UserSummaryDto>>.Success(users);
    }

    public async Task<Result<PagedResult<RoleSummaryDto>>> ListRolesAsync(
        Guid? tenantId,
        PageRequest pageRequest,
        bool includeSystemRoles = true,
        CancellationToken cancellationToken = default)
    {
        var roles = await repository.ListRolesAsync(tenantId, pageRequest, includeSystemRoles, cancellationToken);
        return Result<PagedResult<RoleSummaryDto>>.Success(roles);
    }

    public async Task<Result<UserSummaryDto>> CreateUserAsync(
        CreateUserCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationError = await ValidateCreateAsync(command, cancellationToken);
        if (validationError is not null)
        {
            return Result<UserSummaryDto>.Failure(validationError);
        }

        var role = await repository.GetRoleAsync(command.RoleId, cancellationToken);
        if (role is null)
        {
            return Result<UserSummaryDto>.Failure("Role was not found.");
        }

        var user = await repository.CreateUserAsync(new CreateUserData(
            command.FullName.Trim(),
            command.Email.Trim(),
            NormalizeEmail(command.Email),
            TrimToNull(command.Phone),
            passwordHasher.HashPassword(command.TemporaryPassword),
            command.RoleId,
            role.Scope,
            command.TenantId,
            command.StoreId,
            command.RequestedByUserId,
            command.IpAddress,
            command.UserAgent), cancellationToken);

        return Result<UserSummaryDto>.Success(user);
    }

    public async Task<Result<UserSummaryDto>> UpdateUserAsync(
        UpdateUserCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationError = await ValidateUpdateAsync(command, cancellationToken);
        if (validationError is not null)
        {
            return Result<UserSummaryDto>.Failure(validationError);
        }

        var role = await repository.GetRoleAsync(command.RoleId, cancellationToken);
        if (role is null)
        {
            return Result<UserSummaryDto>.Failure("Role was not found.");
        }

        var user = await repository.UpdateUserAsync(new UpdateUserData(
            command.UserId,
            command.FullName.Trim(),
            command.Email.Trim(),
            NormalizeEmail(command.Email),
            TrimToNull(command.Phone),
            command.RoleId,
            role.Scope,
            command.TenantId,
            command.StoreId,
            command.RequestedByUserId,
            command.IpAddress,
            command.UserAgent), cancellationToken);

        return user is null
            ? Result<UserSummaryDto>.Failure("User was not found.")
            : Result<UserSummaryDto>.Success(user);
    }

    public async Task<Result> DisableUserAsync(
        DisableUserCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.UserId == Guid.Empty)
        {
            return Result.Failure("UserId is required.");
        }

        if (!command.RequestedByIsSuperAdmin)
        {
            if (await repository.UserHasSystemRoleAsync(command.UserId, cancellationToken))
            {
                return Result.Failure("Tenant administrators cannot disable SuperAdmin users.");
            }

            if (command.TenantId is null)
            {
                return Result.Failure("Tenant context is required.");
            }

            if (!await repository.UserBelongsToTenantAsync(command.UserId, command.TenantId.Value, cancellationToken))
            {
                return Result.Failure("User does not belong to this tenant.");
            }
        }

        var disabled = await repository.DisableUserAsync(command.UserId, cancellationToken);
        return disabled ? Result.Success() : Result.Failure("User was not found.");
    }

    public async Task<Result> ActivateUserAsync(
        DisableUserCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.UserId == Guid.Empty)
        {
            return Result.Failure("UserId is required.");
        }

        if (!command.RequestedByIsSuperAdmin)
        {
            if (await repository.UserHasSystemRoleAsync(command.UserId, cancellationToken))
            {
                return Result.Failure("Tenant administrators cannot activate SuperAdmin users.");
            }

            if (command.TenantId is null)
            {
                return Result.Failure("Tenant context is required.");
            }

            if (!await repository.UserBelongsToTenantAsync(command.UserId, command.TenantId.Value, cancellationToken))
            {
                return Result.Failure("User does not belong to this tenant.");
            }
        }

        var activated = await repository.ActivateUserAsync(command.UserId, cancellationToken);
        return activated ? Result.Success() : Result.Failure("User was not found.");
    }

    public async Task<Result> ChangePasswordAsync(
        ChangeUserPasswordCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.UserId == Guid.Empty)
        {
            return Result.Failure("UserId is required.");
        }

        if (string.IsNullOrWhiteSpace(command.NewPassword) || command.NewPassword.Length < 10)
        {
            return Result.Failure("NewPassword must have at least 10 characters.");
        }

        if (!command.NewPassword.Equals(command.ConfirmPassword, StringComparison.Ordinal))
        {
            return Result.Failure("Passwords do not match.");
        }

        if (!command.RequestedByIsSuperAdmin)
        {
            if (await repository.UserHasSystemRoleAsync(command.UserId, cancellationToken))
            {
                return Result.Failure("Tenant administrators cannot change SuperAdmin passwords.");
            }

            if (command.TenantId is null)
            {
                return Result.Failure("Tenant context is required.");
            }

            if (!await repository.UserBelongsToTenantAsync(command.UserId, command.TenantId.Value, cancellationToken))
            {
                return Result.Failure("User does not belong to this tenant.");
            }
        }

        var changed = await repository.ChangePasswordAsync(new ChangeUserPasswordData(
            command.UserId,
            passwordHasher.HashPassword(command.NewPassword),
            command.TenantId,
            command.RequestedByUserId,
            command.IpAddress,
            command.UserAgent), cancellationToken);

        return changed ? Result.Success() : Result.Failure("User was not found.");
    }

    private async Task<string?> ValidateCreateAsync(
        CreateUserCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.FullName))
        {
            return "FullName is required.";
        }

        if (string.IsNullOrWhiteSpace(command.Email) || !command.Email.Contains('@', StringComparison.Ordinal))
        {
            return "A valid email is required.";
        }

        if (string.IsNullOrWhiteSpace(command.TemporaryPassword) || command.TemporaryPassword.Length < 10)
        {
            return "TemporaryPassword must have at least 10 characters.";
        }

        if (command.RoleId == Guid.Empty)
        {
            return "RoleId is required.";
        }

        if (await repository.UserEmailExistsAsync(NormalizeEmail(command.Email), cancellationToken))
        {
            return "Email is already in use.";
        }

        var role = await repository.GetRoleAsync(command.RoleId, cancellationToken);
        if (role is null || !role.IsActive)
        {
            return "Role was not found.";
        }

        if (!command.RequestedByIsSuperAdmin)
        {
            if (role.Scope == "System" || role.IsSystemRole)
            {
                return "Tenant administrators cannot assign system roles.";
            }

            if (command.TenantId is null)
            {
                return "Tenant context is required.";
            }
        }

        if (role.Scope == "System")
        {
            if (command.TenantId is not null || command.StoreId is not null)
            {
                return "System roles cannot be scoped to tenant or store.";
            }
        }
        else if (role.Scope == "Tenant")
        {
            if (command.TenantId is null || command.StoreId is not null)
            {
                return "Tenant roles require TenantId and cannot use StoreId.";
            }
        }
        else if (role.Scope == "Store")
        {
            if (command.TenantId is null || command.StoreId is null)
            {
                return "Store roles require TenantId and StoreId.";
            }
        }
        else
        {
            return "Role scope is invalid.";
        }

        if (command.TenantId is not null
            && !await repository.TenantExistsAsync(command.TenantId.Value, cancellationToken))
        {
            return "Tenant was not found.";
        }

        if (command.TenantId is not null
            && command.StoreId is not null
            && !await repository.StoreBelongsToTenantAsync(command.TenantId.Value, command.StoreId.Value, cancellationToken))
        {
            return "Store was not found for this tenant.";
        }

        if (role.TenantId is not null && role.TenantId != command.TenantId)
        {
            return "Role does not belong to this tenant.";
        }

        return null;
    }

    private async Task<string?> ValidateUpdateAsync(
        UpdateUserCommand command,
        CancellationToken cancellationToken)
    {
        if (command.UserId == Guid.Empty)
        {
            return "UserId is required.";
        }

        if (string.IsNullOrWhiteSpace(command.FullName))
        {
            return "FullName is required.";
        }

        if (string.IsNullOrWhiteSpace(command.Email) || !command.Email.Contains('@', StringComparison.Ordinal))
        {
            return "A valid email is required.";
        }

        if (command.RoleId == Guid.Empty)
        {
            return "RoleId is required.";
        }

        if (await repository.UserEmailExistsAsync(NormalizeEmail(command.Email), command.UserId, cancellationToken))
        {
            return "Email is already in use.";
        }

        var role = await repository.GetRoleAsync(command.RoleId, cancellationToken);
        if (role is null || !role.IsActive)
        {
            return "Role was not found.";
        }

        if (!command.RequestedByIsSuperAdmin)
        {
            if (await repository.UserHasSystemRoleAsync(command.UserId, cancellationToken))
            {
                return "Tenant administrators cannot edit SuperAdmin users.";
            }

            if (role.Scope == "System" || role.IsSystemRole)
            {
                return "Tenant administrators cannot assign system roles.";
            }

            if (command.TenantId is null)
            {
                return "Tenant context is required.";
            }

            if (!await repository.UserBelongsToTenantAsync(command.UserId, command.TenantId.Value, cancellationToken))
            {
                return "User does not belong to this tenant.";
            }
        }

        if (role.Scope == "System")
        {
            if (command.TenantId is not null || command.StoreId is not null)
            {
                return "System roles cannot be scoped to tenant or store.";
            }
        }
        else if (role.Scope == "Tenant")
        {
            if (command.TenantId is null || command.StoreId is not null)
            {
                return "Tenant roles require TenantId and cannot use StoreId.";
            }
        }
        else if (role.Scope == "Store")
        {
            if (command.TenantId is null || command.StoreId is null)
            {
                return "Store roles require TenantId and StoreId.";
            }
        }
        else
        {
            return "Role scope is invalid.";
        }

        if (command.TenantId is not null
            && !await repository.TenantExistsAsync(command.TenantId.Value, cancellationToken))
        {
            return "Tenant was not found.";
        }

        if (command.TenantId is not null
            && command.StoreId is not null
            && !await repository.StoreBelongsToTenantAsync(command.TenantId.Value, command.StoreId.Value, cancellationToken))
        {
            return "Store was not found for this tenant.";
        }

        if (role.TenantId is not null && role.TenantId != command.TenantId)
        {
            return "Role does not belong to this tenant.";
        }

        return null;
    }

    private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
