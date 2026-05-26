namespace Arca.Application.Catalog;

public sealed class SelectedVariantAttribute
{
    public Guid ProductAttributeId { get; init; }
    public List<Guid> ProductAttributeValueIds { get; init; } = [];
}

public sealed record VariantAttributeValueInfo(
    Guid ProductAttributeId,
    string AttributeName,
    int AttributeSortOrder,
    Guid ProductAttributeValueId,
    string ValueName,
    string ValueCode,
    int ValueSortOrder);

public sealed record VariantAttributeSelection(
    Guid ProductAttributeId,
    string AttributeName,
    Guid ProductAttributeValueId,
    string ValueName,
    string ValueCode);

public sealed record GeneratedProductVariant(
    string Sku,
    string Name,
    decimal DefaultSalePrice,
    decimal? DefaultCostPrice,
    string Status,
    IReadOnlyCollection<VariantAttributeSelection> Attributes);
