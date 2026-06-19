namespace Arca.Application.Auth;

public sealed record AuthenticateUserCommand(
    string Email,
    string Password,
    string? IpAddress,
    string? UserAgent);
