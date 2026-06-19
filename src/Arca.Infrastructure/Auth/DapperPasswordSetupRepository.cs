using Arca.Application.Abstractions.Auth;
using Arca.Infrastructure.Database;
using Dapper;

namespace Arca.Infrastructure.Auth;

public sealed class DapperPasswordSetupRepository(IDbConnectionFactory connectionFactory) : IPasswordSetupRepository
{
    public async Task StoreTokenAsync(
        Guid userId,
        string tokenHash,
        DateTime expiresAt,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO password_setup_token (id, user_id, token_hash, expires_at, created_at)
            VALUES (@Id, @UserId, @TokenHash, @ExpiresAt, @CreatedAt);
            """,
            new
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TokenHash = tokenHash,
                ExpiresAt = expiresAt,
                CreatedAt = DateTime.UtcNow
            },
            cancellationToken: cancellationToken));
    }

    public async Task<Guid?> GetValidUserIdAsync(
        string tokenHash,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            """
            SELECT user_id
            FROM password_setup_token
            WHERE token_hash = @TokenHash
              AND used_at IS NULL
              AND expires_at > @Now;
            """,
            new { TokenHash = tokenHash, Now = now },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> SetPasswordAsync(
        Guid userId,
        string tokenHash,
        string passwordHash,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var affectedToken = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE password_setup_token
            SET used_at = @Now
            WHERE token_hash = @TokenHash
              AND user_id = @UserId
              AND used_at IS NULL
              AND expires_at > @Now;
            """,
            new { UserId = userId, TokenHash = tokenHash, Now = now },
            transaction,
            cancellationToken: cancellationToken));

        if (affectedToken == 0)
        {
            transaction.Rollback();
            return false;
        }

        var affectedUser = await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE app_user
            SET password_hash = @PasswordHash,
                email_confirmed = TRUE,
                updated_at = @Now
            WHERE id = @UserId
              AND is_active = TRUE;
            """,
            new { UserId = userId, PasswordHash = passwordHash, Now = now },
            transaction,
            cancellationToken: cancellationToken));

        if (affectedUser == 0)
        {
            transaction.Rollback();
            return false;
        }

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO audit_log (
                id, user_id, tenant_id, store_id, action, entity_name, entity_id,
                old_value, new_value, ip_address, user_agent, created_at
            )
            VALUES (
                @Id, @UserId, NULL, NULL, 'users.set_initial_password', 'User', @UserId,
                NULL, 'PasswordSet=True', NULL, NULL, @CreatedAt
            );
            """,
            new { Id = Guid.NewGuid(), UserId = userId, CreatedAt = now },
            transaction,
            cancellationToken: cancellationToken));

        transaction.Commit();
        return true;
    }
}
