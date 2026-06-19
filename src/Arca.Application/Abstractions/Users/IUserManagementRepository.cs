using Arca.Application.Common;
using Arca.Application.Users;

namespace Arca.Application.Abstractions.Users;

public interface IUserManagementRepository
{
    Task<PagedResult<UserSummaryDto>> ListUsersAsync(Guid? tenantId, PageRequest pageRequest, CancellationToken cancellationToken = default);

    Task<PagedResult<RoleSummaryDto>> ListRolesAsync(
        Guid? tenantId,
        PageRequest pageRequest,
        bool includeSystemRoles = true,
        CancellationToken cancellationToken = default);

    Task<bool> UserEmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken = default);

    Task<bool> UserEmailExistsAsync(string normalizedEmail, Guid excludingUserId, CancellationToken cancellationToken = default);

    Task<bool> TenantExistsAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<bool> StoreBelongsToTenantAsync(Guid tenantId, Guid storeId, CancellationToken cancellationToken = default);

    Task<RoleSummaryDto?> GetRoleAsync(Guid roleId, CancellationToken cancellationToken = default);

    Task<UserSummaryDto> CreateUserAsync(CreateUserData data, CancellationToken cancellationToken = default);

    Task<UserSummaryDto?> UpdateUserAsync(UpdateUserData data, CancellationToken cancellationToken = default);

    Task<bool> UserHasSystemRoleAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> UserBelongsToTenantAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default);

    Task<bool> ChangePasswordAsync(ChangeUserPasswordData data, CancellationToken cancellationToken = default);

    Task<bool> DisableUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> ActivateUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
