using System.Security.Cryptography;
using Arca.Application.Abstractions.Auth;
using Arca.Application.Common;
using Arca.Application.Security;

namespace Arca.Application.Auth;

public sealed class PasswordSetupService(
    IPasswordSetupRepository repository,
    IPasswordHasher passwordHasher)
{
    public async Task<Result<PasswordSetupTokenResult>> CreateTokenAsync(
        Guid userId,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return Result<PasswordSetupTokenResult>.Failure("UserId is required.");
        }

        var token = CreateToken();
        var expiresAt = DateTime.UtcNow.Add(ttl);
        await repository.StoreTokenAsync(userId, HashToken(token), expiresAt, cancellationToken);
        return Result<PasswordSetupTokenResult>.Success(new PasswordSetupTokenResult(token, expiresAt));
    }

    public async Task<Result> SetPasswordAsync(
        string token,
        string newPassword,
        string confirmPassword,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Result.Failure("Password setup token is required.");
        }

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 10)
        {
            return Result.Failure("Password must have at least 10 characters.");
        }

        if (!newPassword.Equals(confirmPassword, StringComparison.Ordinal))
        {
            return Result.Failure("Passwords do not match.");
        }

        var tokenHash = HashToken(token);
        var now = DateTime.UtcNow;
        var userId = await repository.GetValidUserIdAsync(tokenHash, now, cancellationToken);
        if (userId is null)
        {
            return Result.Failure("Password setup link is invalid or expired.");
        }

        var updated = await repository.SetPasswordAsync(
            userId.Value,
            tokenHash,
            passwordHasher.HashPassword(newPassword),
            now,
            cancellationToken);

        return updated ? Result.Success() : Result.Failure("Password setup link is invalid or expired.");
    }

    private static string CreateToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-", StringComparison.Ordinal)
            .Replace("/", "_", StringComparison.Ordinal)
            .TrimEnd('=');
    }

    private static string HashToken(string token)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}

public sealed record PasswordSetupTokenResult(string Token, DateTime ExpiresAt);
