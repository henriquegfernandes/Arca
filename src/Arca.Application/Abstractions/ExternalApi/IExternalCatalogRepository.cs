using Arca.Application.ExternalApi;

namespace Arca.Application.Abstractions.ExternalApi;

public interface IExternalCatalogRepository
{
    Task<IReadOnlyCollection<ExternalCategoryDto>> ListCategoriesAsync(
        ExternalApiClientContext client,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ExternalProductListItemDto>> ListProductsAsync(
        ExternalApiClientContext client,
        CancellationToken cancellationToken = default);

    Task<ExternalProductDto?> GetProductAsync(
        ExternalApiClientContext client,
        Guid productId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ExternalProductVariantDto>> ListProductVariantsAsync(
        ExternalApiClientContext client,
        Guid productId,
        CancellationToken cancellationToken = default);

    Task<ExternalProductVariantDto?> GetVariantAsync(
        ExternalApiClientContext client,
        Guid variantId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ExternalProductImageDto>> ListVariantImagesAsync(
        ExternalApiClientContext client,
        Guid variantId,
        CancellationToken cancellationToken = default);

    Task<ExternalInventoryAvailabilityDto?> GetAvailabilityAsync(
        ExternalApiClientContext client,
        Guid variantId,
        CancellationToken cancellationToken = default);
}
