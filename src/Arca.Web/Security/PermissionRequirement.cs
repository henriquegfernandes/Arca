using Microsoft.AspNetCore.Authorization;

namespace Arca.Web.Security;

public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
