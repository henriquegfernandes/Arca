namespace Arca.Application.Abstractions.Auth;

public interface IPasswordSetupRepository
{
    Task StoreTokenAsync(Guid userId, string tokenHash, DateTime expiresAt, CancellationToken cancellationToken = default);
    Task<Guid?> GetValidUserIdAsync(string tokenHash, DateTime now, CancellationToken cancellationToken = default);
    Task<bool> SetPasswordAsync(Guid userId, string tokenHash, string passwordHash, DateTime now, CancellationToken cancellationToken = default);
}
