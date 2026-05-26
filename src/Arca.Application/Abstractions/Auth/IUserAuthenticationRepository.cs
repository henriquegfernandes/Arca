using Arca.Application.Auth;

namespace Arca.Application.Abstractions.Auth;

public interface IUserAuthenticationRepository
{
    Task<UserCredentials?> FindByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);
    Task UpdateLastLoginAsync(Guid userId, CancellationToken cancellationToken = default);
    Task RecordLoginAttemptAsync(
        string email,
        bool success,
        string? failureReason,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);
}
