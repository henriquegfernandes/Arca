using Arca.Application.Abstractions.Auth;
using Arca.Application.Auth;
using Arca.Infrastructure.Database;
using Dapper;

namespace Arca.Infrastructure.Auth;

public sealed class DapperUserAuthenticationRepository(IDbConnectionFactory connectionFactory) : IUserAuthenticationRepository
{
    public async Task<UserCredentials?> FindByNormalizedEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();

        using var result = await connection.QueryMultipleAsync(new CommandDefinition(
            """
            SELECT
                id AS Id,
                full_name AS FullName,
                email AS Email,
                password_hash AS PasswordHash,
                is_active AS IsActive
            FROM app_user
            WHERE normalized_email = @NormalizedEmail;

            SELECT
                r.name AS Name,
                r.scope AS Scope
            FROM app_user u
            INNER JOIN user_role ur ON ur.user_id = u.id
            INNER JOIN role r ON r.id = ur.role_id
            WHERE u.normalized_email = @NormalizedEmail
              AND r.is_active = TRUE;
            """,
            new { NormalizedEmail = normalizedEmail },
            cancellationToken: cancellationToken));

        var user = await result.ReadSingleOrDefaultAsync<UserRecord>();
        if (user is null)
        {
            return null;
        }

        var roles = (await result.ReadAsync<UserRoleSummary>()).ToArray();
        return new UserCredentials(
            user.Id,
            user.FullName,
            user.Email,
            user.PasswordHash,
            user.IsActive,
            roles);
    }

    public async Task UpdateLastLoginAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE app_user SET last_login_at = @LastLoginAt, updated_at = @LastLoginAt WHERE id = @UserId;",
            new { UserId = userId, LastLoginAt = DateTime.UtcNow },
            cancellationToken: cancellationToken));
    }

    public async Task RecordLoginAttemptAsync(
        string email,
        bool success,
        string? failureReason,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO login_attempt (id, email, success, failure_reason, ip_address, user_agent, created_at)
            VALUES (@Id, @Email, @Success, @FailureReason, @IpAddress, @UserAgent, @CreatedAt);
            """,
            new
            {
                Id = Guid.NewGuid(),
                Email = email,
                Success = success,
                FailureReason = failureReason,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                CreatedAt = DateTime.UtcNow
            },
            cancellationToken: cancellationToken));
    }

    private sealed class UserRecord
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}
