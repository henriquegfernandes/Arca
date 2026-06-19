namespace Arca.Application.Catalog;

public class PreviewProductVariantsCommand
{
    public Guid TenantId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string BaseSku { get; init; } = string.Empty;
    public decimal DefaultSalePrice { get; init; }
    public decimal? DefaultCostPrice { get; init; }
    public string Status { get; init; } = "Active";
    public List<SelectedVariantAttribute> VariantAttributes { get; init; } = [];
}

public sealed class CreateProductCommand : PreviewProductVariantsCommand
{
    public Guid? CategoryId { get; init; }
    public Guid? ProductTypeId { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Barcode { get; init; }
    public string? Brand { get; init; }
    public Guid? RequestedByUserId { get; init; }
}

public sealed class AddProductVariantsCommand : PreviewProductVariantsCommand
{
    public Guid ProductId { get; init; }
    public Guid? RequestedByUserId { get; init; }
}

public sealed record ProductVariantPreviewResult(IReadOnlyCollection<GeneratedProductVariant> Variants);

public sealed record CreateProductResult(
    Guid ProductId,
    IReadOnlyCollection<CreatedProductVariant> Variants);

public sealed record CreatedProductVariant(Guid Id, string Sku, string Name);

public sealed record CreateProductData(
    CreateProductCommand Command,
    IReadOnlyCollection<SelectedVariantAttribute> VariantOptions,
    IReadOnlyCollection<GeneratedProductVariant> Variants);

public sealed record AddProductVariantsData(
    AddProductVariantsCommand Command,
    IReadOnlyCollection<SelectedVariantAttribute> VariantOptions,
    IReadOnlyCollection<GeneratedProductVariant> Variants);
