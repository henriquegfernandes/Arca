namespace Arca.Application.Auth;

public sealed record UserCredentials(
    Guid Id,
    string FullName,
    string Email,
    string PasswordHash,
    bool IsActive,
    IReadOnlyCollection<UserRoleSummary> Roles);

public sealed record UserRoleSummary(string Name, string Scope);
