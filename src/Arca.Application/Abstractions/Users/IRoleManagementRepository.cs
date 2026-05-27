using Arca.Application.Common;
using Arca.Application.Users;

namespace Arca.Application.Abstractions.Users;

public interface IRoleManagementRepository
{
    Task<IReadOnlyCollection<PermissionDto>> ListPermissionsAsync(CancellationToken cancellationToken = default);

    Task<PagedResult<RoleDetailsDto>> ListRolesAsync(Guid? tenantId, PageRequest pageRequest, CancellationToken cancellationToken = default);

    Task<RoleDetailsDto?> GetRoleAsync(Guid roleId, CancellationToken cancellationToken = default);

    Task<bool> TenantExistsAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<bool> RoleNameExistsAsync(Guid? tenantId, string normalizedName, Guid? exceptRoleId = null, CancellationToken cancellationToken = default);

    Task<RoleDetailsDto> CreateRoleAsync(CreateRoleData data, CancellationToken cancellationToken = default);

    Task<RoleDetailsDto?> UpdateRolePermissionsAsync(UpdateRolePermissionsData data, CancellationToken cancellationToken = default);

    Task<bool> DisableRoleAsync(Guid roleId, CancellationToken cancellationToken = default);
}
