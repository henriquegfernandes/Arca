using Arca.Application.Security;

namespace Arca.Application.Tenancy;

public sealed class UserProvisioningService(IPasswordHasher passwordHasher)
{
    public TenantAdminProvisioning CreateTenantAdmin(AdministratorSetupStep administrator)
    {
        var password = administrator.TemporaryPassword;
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("A temporary password is required until invite emails are implemented.");
        }

        return new TenantAdminProvisioning(
            NormalizeEmail(administrator.Email),
            passwordHasher.HashPassword(password));
    }

    private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();
}

public sealed record TenantAdminProvisioning(string NormalizedEmail, string PasswordHash);
