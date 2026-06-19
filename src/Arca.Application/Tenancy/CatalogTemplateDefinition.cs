namespace Arca.Application.Tenancy;

public sealed record CatalogTemplateDefinition(
    string Name,
    IReadOnlyCollection<CategorySeed> Categories,
    IReadOnlyCollection<ProductAttributeSeed> Attributes,
    IReadOnlyCollection<ProductTypeSeed> ProductTypes);

public sealed record CategorySeed(string Name, string Slug, string? Description, int SortOrder);

public sealed record ProductAttributeSeed(
    string Name,
    string Code,
    string AttributeType,
    bool IsVariantAttribute,
    bool IsRequired,
    int SortOrder,
    IReadOnlyCollection<ProductAttributeValueSeed> Values);

public sealed record ProductAttributeValueSeed(
    string Name,
    string Code,
    string? Value,
    string? HexCode,
    int SortOrder);

public sealed record ProductTypeSeed(
    string Name,
    string? Description,
    IReadOnlyCollection<ProductTypeAttributeSeed> Attributes);

public sealed record ProductTypeAttributeSeed(
    string AttributeCode,
    bool IsRequired,
    bool IsVariantAttribute,
    int SortOrder);
