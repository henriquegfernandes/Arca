using Arca.Application.Abstractions.Catalog;
using Arca.Application.Common;

namespace Arca.Application.Catalog;

public sealed class ProductCatalogService(
    IProductCatalogRepository repository,
    IProductVariantGenerator variantGenerator)
{
    public async Task<Result<ProductVariantPreviewResult>> PreviewVariantsAsync(
        PreviewProductVariantsCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidatePreview(command);
        if (validationError is not null)
        {
            return Result<ProductVariantPreviewResult>.Failure(validationError);
        }

        var selectedValues = await LoadSelectedValuesAsync(command.TenantId, command.VariantAttributes, cancellationToken);
        var preliminaryVariants = variantGenerator.GenerateVariants(new ProductVariantGenerationInput(
            command.ProductName,
            command.BaseSku,
            command.DefaultSalePrice,
            command.DefaultCostPrice,
            NormalizeStatus(command.Status),
            selectedValues,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)));

        var existingSkus = await repository.GetExistingSkusAsync(
            preliminaryVariants.Select(variant => variant.Sku).ToArray(),
            cancellationToken);

        var variants = variantGenerator.GenerateVariants(new ProductVariantGenerationInput(
            command.ProductName,
            command.BaseSku,
            command.DefaultSalePrice,
            command.DefaultCostPrice,
            NormalizeStatus(command.Status),
            selectedValues,
            existingSkus));

        return Result<ProductVariantPreviewResult>.Success(new ProductVariantPreviewResult(variants));
    }

    public async Task<Result<CreateProductResult>> CreateProductAsync(
        CreateProductCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateCreate(command);
        if (validationError is not null)
        {
            return Result<CreateProductResult>.Failure(validationError);
        }

        var slug = NormalizeSlug(command.Slug);
        if (await repository.ProductSlugExistsAsync(command.TenantId, slug, cancellationToken))
        {
            return Result<CreateProductResult>.Failure("Product slug is already in use for this tenant.");
        }

        var selectedValues = await LoadSelectedValuesAsync(command.TenantId, command.VariantAttributes, cancellationToken);
        var preview = await PreviewVariantsAsync(command, cancellationToken);
        if (preview.IsFailure || preview.Value is null)
        {
            return Result<CreateProductResult>.Failure(preview.Error ?? "Could not generate product variants.");
        }

        if (preview.Value.Variants.Count == 0)
        {
            return Result<CreateProductResult>.Failure("No new variants were generated. Check duplicate SKUs.");
        }

        var normalizedCommand = new CreateProductCommand
        {
            TenantId = command.TenantId,
            CategoryId = command.CategoryId,
            ProductTypeId = command.ProductTypeId,
            ProductName = command.ProductName.Trim(),
            Slug = slug,
            Description = TrimToNull(command.Description),
            BaseSku = NormalizeSku(command.BaseSku),
            Barcode = TrimToNull(command.Barcode),
            Brand = TrimToNull(command.Brand),
            Status = NormalizeStatus(command.Status),
            DefaultSalePrice = command.DefaultSalePrice,
            DefaultCostPrice = command.DefaultCostPrice,
            VariantAttributes = command.VariantAttributes,
            RequestedByUserId = command.RequestedByUserId
        };

        var result = await repository.CreateProductAsync(
            new CreateProductData(normalizedCommand, command.VariantAttributes, preview.Value.Variants),
            cancellationToken);

        return Result<CreateProductResult>.Success(result);
    }

    private async Task<IReadOnlyCollection<IReadOnlyCollection<VariantAttributeValueInfo>>> LoadSelectedValuesAsync(
        Guid tenantId,
        IReadOnlyCollection<SelectedVariantAttribute> selectedAttributes,
        CancellationToken cancellationToken)
    {
        var values = await repository.GetVariantAttributeValuesAsync(tenantId, selectedAttributes, cancellationToken);

        return selectedAttributes
            .Select(attribute => values
                .Where(value => value.ProductAttributeId == attribute.ProductAttributeId)
                .OrderBy(value => value.AttributeSortOrder)
                .ThenBy(value => value.ValueSortOrder)
                .ToArray())
            .Where(attributeValues => attributeValues.Length > 0)
            .ToArray();
    }

    private static string? ValidateCreate(CreateProductCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Slug))
        {
            return "Product slug is required.";
        }

        return ValidatePreview(command);
    }

    private static string? ValidatePreview(PreviewProductVariantsCommand command)
    {
        if (command.TenantId == Guid.Empty)
        {
            return "TenantId is required.";
        }

        if (string.IsNullOrWhiteSpace(command.ProductName))
        {
            return "ProductName is required.";
        }

        if (string.IsNullOrWhiteSpace(command.BaseSku))
        {
            return "BaseSku is required.";
        }

        if (command.DefaultSalePrice < 0)
        {
            return "DefaultSalePrice cannot be negative.";
        }

        if (command.VariantAttributes.Any(attribute => attribute.ProductAttributeId == Guid.Empty))
        {
            return "Variant attribute id is required.";
        }

        if (command.VariantAttributes.Any(attribute => attribute.ProductAttributeValueIds.Any(valueId => valueId == Guid.Empty)))
        {
            return "Variant attribute value id is required.";
        }

        var status = NormalizeStatus(command.Status);
        if (status is not ("Draft" or "Active" or "Inactive"))
        {
            return "Status must be Draft, Active or Inactive.";
        }

        return null;
    }

    private static string NormalizeSku(string value) =>
        value.Trim().ToUpperInvariant().Replace(" ", "-", StringComparison.Ordinal);

    private static string NormalizeSlug(string value) => value.Trim().ToLowerInvariant();

    private static string NormalizeStatus(string status)
    {
        var normalized = status.Trim();
        return normalized.Equals("draft", StringComparison.OrdinalIgnoreCase) ? "Draft"
            : normalized.Equals("inactive", StringComparison.OrdinalIgnoreCase) ? "Inactive"
            : "Active";
    }

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
