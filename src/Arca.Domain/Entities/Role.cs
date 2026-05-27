using Arca.Domain.Enums;

namespace Arca.Domain.Entities;

public sealed class Role : BaseEntity
{
    public Guid? TenantId { get; private set; }
    public string Name { get; private set; }
    public string NormalizedName { get; private set; }
    public string? Description { get; private set; }
    public RoleScope Scope { get; private set; }
    public bool IsSystemRole { get; private set; }
    public bool IsActive { get; private set; }

    public Role(Guid? tenantId, string name, RoleScope scope, bool isSystemRole, string? description = null)
    {
        if (scope == RoleScope.System && tenantId is not null)
        {
            throw new ArgumentException("System roles cannot belong to a tenant.", nameof(tenantId));
        }

        if (scope != RoleScope.System && tenantId is null)
        {
            throw new ArgumentException("Tenant and store roles must belong to a tenant.", nameof(tenantId));
        }

        TenantId = tenantId;
        Name = RequireText(name, nameof(name));
        NormalizedName = Name.ToUpperInvariant();
        Description = description;
        Scope = scope;
        IsSystemRole = isSystemRole;
        IsActive = true;
    }

    private static string RequireText(string value, string name) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{name} is required.", name) : value.Trim();
}
