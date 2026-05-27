using Arca.Application.Catalog;

namespace Arca.Application.Abstractions.Catalog;

public interface IProductImageRepository
{
    Task<bool> ProductBelongsToTenantAsync(Guid tenantId, Guid productId, CancellationToken cancellationToken = default);

    Task<bool> VariantBelongsToProductAsync(
        Guid productId,
        Guid productVariantId,
        CancellationToken cancellationToken = default);

    Task<ProductImageDto> AddAsync(AddProductImageData imageData, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ProductImageDto>> ListAsync(
        Guid tenantId,
        Guid productId,
        CancellationToken cancellationToken = default);

    Task<ProductImageDto?> FindAsync(
        Guid tenantId,
        Guid productId,
        Guid imageId,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid tenantId,
        Guid productId,
        Guid imageId,
        Guid? requestedByUserId,
        CancellationToken cancellationToken = default);
}
