namespace Arca.Application.Security;

public interface IPermissionService
{
    Task<bool> HasPermissionAsync(
        Guid userId,
        string permission,
        Guid? tenantId,
        Guid? storeId,
        CancellationToken cancellationToken = default);
}
