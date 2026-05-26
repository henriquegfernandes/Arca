using Arca.Application.Abstractions.Tenancy;
using Arca.Application.Common;

namespace Arca.Application.Tenancy;

public sealed class TenantSetupService(
    ITenantSetupRepository repository,
    UserProvisioningService userProvisioningService,
    CatalogTemplateSeeder catalogTemplateSeeder)
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

        if (string.IsNullOrWhiteSpace(command.Administrator.TemporaryPassword))
        {
            return "TemporaryPassword is required until invite emails are implemented.";
        }

        if (!CatalogTemplateSeeder.IsKnownTemplate(command.Catalog.Template))
        {
            return "Catalog template is invalid.";
        }

        return null;
    }

    private static string NormalizeSlug(string value) => value.Trim().ToLowerInvariant();

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
