using Arca.Application.Security;
using Arca.Infrastructure.Database;
using Dapper;
using Microsoft.Extensions.Configuration;

namespace Arca.Infrastructure.Seed;

public sealed class SuperAdminSeedService(
    IDbConnectionFactory connectionFactory,
    IPasswordHasher passwordHasher,
    IConfiguration configuration) : ISuperAdminSeedService
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!bool.TryParse(configuration["Seed:SuperAdmin:Enabled"], out var seedEnabled) || !seedEnabled)
        {
            return;
        }

        var fullName = configuration["Seed:SuperAdmin:FullName"] ?? "Arca Super Admin";
        var email = configuration["Seed:SuperAdmin:Email"] ?? "admin@arca.local";
        var password = configuration["Seed:SuperAdmin:Password"]
            ?? throw new InvalidOperationException("Seed:SuperAdmin:Password must be configured when SuperAdmin seed is enabled.");

        using var connection = connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            foreach (var permission in KnownPermissions.All)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO permission (id, name, description, module)
                    VALUES (@Id, @Name, @Description, @Module)
                    ON CONFLICT (name)
                    DO UPDATE SET description = EXCLUDED.description, module = EXCLUDED.module;
                    """,
                    new
                    {
                        Id = Guid.NewGuid(),
                        permission.Name,
                        permission.Description,
                        permission.Module
                    },
                    transaction,
                    cancellationToken: cancellationToken));
            }

            var now = DateTime.UtcNow;
            var roleId = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
                "SELECT id FROM role WHERE tenant_id IS NULL AND normalized_name = 'SUPERADMIN';",
                transaction: transaction,
                cancellationToken: cancellationToken));

            if (roleId is null)
            {
                roleId = Guid.NewGuid();
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO role (
                        id, tenant_id, name, normalized_name, description, scope,
                        is_system_role, is_active, created_at, updated_at
                    )
                    VALUES (
                        @Id, NULL, 'SuperAdmin', 'SUPERADMIN', 'Platform administrator',
                        'System', TRUE, TRUE, @CreatedAt, NULL
                    );
                    """,
                    new { Id = roleId.Value, CreatedAt = now },
                    transaction,
                    cancellationToken: cancellationToken));
            }

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO role_permission (role_id, permission_id)
                SELECT @RoleId, id
                FROM permission
                ON CONFLICT (role_id, permission_id) DO NOTHING;
                """,
                new { RoleId = roleId.Value },
                transaction,
                cancellationToken: cancellationToken));

            var normalizedEmail = NormalizeEmail(email);
            var userId = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
                "SELECT id FROM app_user WHERE normalized_email = @NormalizedEmail;",
                new { NormalizedEmail = normalizedEmail },
                transaction,
                cancellationToken: cancellationToken));

            if (userId is null)
            {
                userId = Guid.NewGuid();
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO app_user (
                        id, full_name, email, normalized_email, phone, password_hash,
                        is_active, email_confirmed, last_login_at, created_at, updated_at
                    )
                    VALUES (
                        @Id, @FullName, @Email, @NormalizedEmail, NULL, @PasswordHash,
                        TRUE, TRUE, NULL, @CreatedAt, NULL
                    );
                    """,
                    new
                    {
                        Id = userId.Value,
                        FullName = fullName,
                        Email = email,
                        NormalizedEmail = normalizedEmail,
                        PasswordHash = passwordHasher.HashPassword(password),
                        CreatedAt = now
                    },
                    transaction,
                    cancellationToken: cancellationToken));
            }

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO user_role (id, user_id, role_id, tenant_id, store_id, created_at)
                SELECT @Id, @UserId, @RoleId, NULL, NULL, @CreatedAt
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM user_role
                    WHERE user_id = @UserId
                      AND role_id = @RoleId
                      AND tenant_id IS NULL
                      AND store_id IS NULL
                );
                """,
                new { Id = Guid.NewGuid(), UserId = userId.Value, RoleId = roleId.Value, CreatedAt = now },
                transaction,
                cancellationToken: cancellationToken));

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();
}
