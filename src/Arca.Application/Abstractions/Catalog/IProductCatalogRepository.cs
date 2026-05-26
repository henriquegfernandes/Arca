using Arca.Application.Catalog;

namespace Arca.Application.Abstractions.Catalog;

public interface IProductCatalogRepository
{
    Task<IReadOnlyCollection<VariantAttributeValueInfo>> GetVariantAttributeValuesAsync(
        Guid tenantId,
        IReadOnlyCollection<SelectedVariantAttribute> selectedAttributes,
        CancellationToken cancellationToken = default);

    Task<IReadOnlySet<string>> GetExistingSkusAsync(
        IReadOnlyCollection<string> skus,
        CancellationToken cancellationToken = default);

    Task<bool> ProductSlugExistsAsync(Guid tenantId, string slug, CancellationToken cancellationToken = default);

    Task<CreateProductResult> CreateProductAsync(
        CreateProductData productData,
        CancellationToken cancellationToken = default);
}
