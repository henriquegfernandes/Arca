namespace Arca.Application.Security;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    bool IsAuthenticated { get; }
    bool IsSuperAdmin { get; }
    Guid? CurrentTenantId { get; }
    Guid? CurrentStoreId { get; }
}
