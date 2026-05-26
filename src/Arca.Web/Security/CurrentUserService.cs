using System.Security.Claims;
using Arca.Application.Security;

namespace Arca.Web.Security;

public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public Guid? UserId => TryGetGuid(ClaimTypes.NameIdentifier);
    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;
    public bool IsSuperAdmin => string.Equals(User?.FindFirstValue("arca:is_super_admin"), bool.TrueString, StringComparison.OrdinalIgnoreCase);
    public Guid? CurrentTenantId => TryGetGuid("arca:tenant_id");
    public Guid? CurrentStoreId => TryGetGuid("arca:store_id");

    private Guid? TryGetGuid(string claimType)
    {
        var value = User?.FindFirstValue(claimType);
        return Guid.TryParse(value, out var parsed) ? parsed : null;
    }
}
