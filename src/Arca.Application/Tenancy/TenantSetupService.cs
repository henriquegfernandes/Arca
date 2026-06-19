using Arca.Application.Abstractions;
using Arca.Application.Abstractions.Tenancy;
using Arca.Application.Common;

namespace Arca.Application.Tenancy;

public sealed class TenantSetupService(
    ITenantSetupRepository repository,
    UserProvisioningService userProvisioningService,
    CatalogTemplateSeeder catalogTemplateSeeder,
    IEmailSender emailSender)
{
    public async Task<Result<TenantSetupResult>> SetupAsync(
        CreateTenantSetupCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationError = Validate(command);
        if (validationError is not null)
        {
            return Result<TenantSetupResult>.Failure(validationError);
        }

        var slug = NormalizeSlug(command.Company.Slug);
        if (await repository.TenantSlugExistsAsync(slug, cancellationToken))
        {
            return Result<TenantSetupResult>.Failure("Tenant slug is already in use.");
        }

        var normalizedCommand = new CreateTenantSetupCommand
        {
            Company = new CompanySetupStep
            {
                Name = command.Company.Name.Trim(),
                LegalName = TrimToNull(command.Company.LegalName),
                Document = TrimToNull(command.Company.Document),
                Slug = slug,
                Email = TrimToNull(command.Company.Email),
                Phone = TrimToNull(command.Company.Phone),
                MainSegment = TrimToNull(command.Company.MainSegment)
            },
            Settings = command.Settings,
            Stores = command.Stores,
            Administrator = command.Administrator,
            Catalog = command.Catalog,
            RequestedByUserId = command.RequestedByUserId,
            IpAddress = command.IpAddress,
            UserAgent = command.UserAgent
        };

        var adminProvisioning = userProvisioningService.CreateTenantAdmin(command.Administrator);
        var template = catalogTemplateSeeder.Build(command.Catalog.Template);

        var result = await repository.CreateAsync(
            new TenantSetupData(
                normalizedCommand,
                adminProvisioning.NormalizedEmail,
                adminProvisioning.PasswordHash,
                template),
            cancellationToken);

        if (command.Administrator.SendInviteEmail && emailSender is not null)
        {
            var subject = $"Welcome to Arca — {command.Company.Name} is ready";
            var body = BuildInviteEmailBody(
                command.Administrator.FullName,
                command.Company.Name,
                command.Administrator.Email,
                adminProvisioning.PlainTextPassword);

            await emailSender.SendAsync(command.Administrator.Email, subject, body, cancellationToken);
        }

        return Result<TenantSetupResult>.Success(result);
    }

    private static string? Validate(CreateTenantSetupCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Company.Name))
        {
            return "Company name is required.";
        }

        if (string.IsNullOrWhiteSpace(command.Company.Slug))
        {
            return "Company slug is required.";
        }

        if (string.IsNullOrWhiteSpace(command.Settings.Currency))
        {
            return "Currency is required.";
        }

        if (string.IsNullOrWhiteSpace(command.Settings.TimeZone))
        {
            return "Time zone is required.";
        }

        if (string.IsNullOrWhiteSpace(command.Settings.DefaultLanguage))
        {
            return "Default language is required.";
        }

        if (command.Stores.Count == 0)
        {
            return "At least one store is required.";
        }

        if (command.Stores.Any(store => string.IsNullOrWhiteSpace(store.Name) || string.IsNullOrWhiteSpace(store.Code)))
        {
            return "Every store must have a name and code.";
        }

        var duplicateStoreCode = command.Stores
            .GroupBy(store => store.Code.Trim().ToUpperInvariant())
            .Any(group => group.Count() > 1);

        if (duplicateStoreCode)
        {
            return "Store codes must be unique within the setup.";
        }

        if (string.IsNullOrWhiteSpace(command.Administrator.FullName)
            || string.IsNullOrWhiteSpace(command.Administrator.Email))
        {
            return "Administrator name and email are required.";
        }

        if (!command.Administrator.SendInviteEmail && string.IsNullOrWhiteSpace(command.Administrator.TemporaryPassword))
        {
            return "A temporary password is required when not sending an invite email.";
        }

        if (!CatalogTemplateSeeder.IsKnownTemplate(command.Catalog.Template))
        {
            return "Catalog template is invalid.";
        }

        return null;
    }

    private static string BuildInviteEmailBody(string fullName, string companyName, string email, string password)
    {
        return $"""
        <!DOCTYPE html>
        <html>
        <head><meta charset="utf-8"></head>
        <body style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background: #f4f6f8; padding: 40px;">
            <div style="max-width: 560px; margin: auto; background: #fff; border-radius: 8px; padding: 32px; box-shadow: 0 4px 12px rgba(0,0,0,0.08);">
                <div style="font-size: 28px; font-weight: 750; margin-bottom: 8px;">Arca</div>
                <p style="color: #5f6b7a; margin: 0 0 24px;">Your inventory management platform</p>
                <h2 style="margin: 0 0 16px;">Welcome, {fullName}!</h2>
                <p style="line-height: 1.6; color: #1f2933; margin: 0 0 16px;">
                    Your company <strong>{companyName}</strong> has been set up in Arca. You can now sign in to the admin panel using the credentials below.
                </p>
                <div style="background: #f8f9fa; border: 1px solid #d7dee5; border-radius: 6px; padding: 16px; margin-bottom: 24px;">
                    <p style="margin: 0 0 8px;"><strong>Email:</strong><br>{email}</p>
                    <p style="margin: 0;"><strong>Temporary password:</strong><br><code style="background: #eef1f4; padding: 2px 6px; border-radius: 4px; font-size: 16px;">{password}</code></p>
                </div>
                <p style="color: #8f9aa8; font-size: 13px; margin: 0;">You will be asked to change your password after signing in for the first time.</p>
            </div>
        </body>
        </html>
        """;
    }

    private static string NormalizeSlug(string value) => value.Trim().ToLowerInvariant();

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
