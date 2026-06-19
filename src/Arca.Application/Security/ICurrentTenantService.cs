namespace Arca.Application.Security;

public interface ICurrentTenantService
{
    Guid? TenantId { get; }
    string? TenantSlug { get; }
}
