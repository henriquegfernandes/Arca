namespace Arca.Application.Auth;

public sealed record AuthenticatedUser(
    Guid Id,
    string FullName,
    string Email,
    bool IsSuperAdmin,
    IReadOnlyCollection<string> Roles);
