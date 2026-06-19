namespace Arca.Application.Security;

public sealed record CurrentUserContextDto(
    Guid Id,
    string FullName,
    string Email,
    bool IsSuperAdmin,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions);

public sealed record TenantContextDto(
    Guid Id,
    string Name,
    string Slug,
    Guid? PrimaryStoreId);

public sealed record StoreContextDto(
    Guid Id,
    Guid TenantId,
    string Name,
    string Code);

public sealed record UserAppContextDto(
    CurrentUserContextDto CurrentUser,
    TenantContextDto? CurrentTenant,
    StoreContextDto? CurrentStore,
    IReadOnlyCollection<TenantContextDto> AvailableTenants,
    IReadOnlyCollection<StoreContextDto> AvailableStores);

public interface IUserContextRepository
{
    Task<UserAppContextDto?> GetAsync(
        Guid userId,
        Guid? selectedTenantId,
        Guid? selectedStoreId,
        CancellationToken cancellationToken = default);
}
