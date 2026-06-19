using Arca.Application.Security;

namespace Arca.Application.Tenancy;

public sealed class UserProvisioningService(IPasswordHasher passwordHasher)
{
    public TenantAdminProvisioning CreateTenantAdmin(AdministratorSetupStep administrator)
    {
        var password = administrator.SendInviteEmail
            ? GeneratePassword()
            : administrator.TemporaryPassword
              ?? throw new InvalidOperationException("A temporary password is required when not sending an invite email.");

        return new TenantAdminProvisioning(
            NormalizeEmail(administrator.Email),
            passwordHasher.HashPassword(password),
            password);
    }

    private static string GeneratePassword()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789";
        var random = Random.Shared;
        var password = new char[12];
        for (var i = 0; i < password.Length; i++)
            password[i] = chars[random.Next(chars.Length)];
        return new string(password);
    }

    private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();
}

public sealed record TenantAdminProvisioning(string NormalizedEmail, string PasswordHash, string PlainTextPassword);
