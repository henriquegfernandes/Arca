namespace Arca.Application.Catalog;

public sealed record ProductVariantGenerationInput(
    string ProductName,
    string BaseSku,
    decimal DefaultSalePrice,
    decimal? DefaultCostPrice,
    string Status,
    IReadOnlyCollection<IReadOnlyCollection<VariantAttributeValueInfo>> SelectedAttributes,
    IReadOnlySet<string> ExistingSkus);

public interface IProductVariantGenerator
{
    IReadOnlyCollection<GeneratedProductVariant> GenerateVariants(ProductVariantGenerationInput input);
}

public sealed class ProductVariantGenerator : IProductVariantGenerator
{
    public IReadOnlyCollection<GeneratedProductVariant> GenerateVariants(ProductVariantGenerationInput input)
    {
        var selectedAttributes = input.SelectedAttributes
            .Where(attribute => attribute.Count > 0)
            .Select(attribute => attribute
                .OrderBy(value => value.AttributeSortOrder)
                .ThenBy(value => value.ValueSortOrder)
                .ToArray())
            .ToArray();

        if (selectedAttributes.Length == 0)
        {
            return
            [
                new(
                    NormalizeSku(input.BaseSku),
                    input.ProductName.Trim(),
                    input.DefaultSalePrice,
                    input.DefaultCostPrice,
                    input.Status,
                    [])
            ];
        }

        var combinations = CartesianProduct(selectedAttributes);
        var generated = new List<GeneratedProductVariant>();
        var generatedSkus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var combination in combinations)
        {
            var sku = BuildSku(input.BaseSku, combination);
            if (input.ExistingSkus.Contains(sku) || !generatedSkus.Add(sku))
            {
                continue;
            }

            var attributes = combination
                .Select(value => new VariantAttributeSelection(
                    value.ProductAttributeId,
                    value.AttributeName,
                    value.ProductAttributeValueId,
                    value.ValueName,
                    value.ValueCode))
                .ToArray();

            generated.Add(new GeneratedProductVariant(
                sku,
                BuildName(input.ProductName, combination),
                input.DefaultSalePrice,
                input.DefaultCostPrice,
                input.Status,
                attributes));
        }

        return generated;
    }

    private static IEnumerable<IReadOnlyCollection<VariantAttributeValueInfo>> CartesianProduct(
        IReadOnlyCollection<VariantAttributeValueInfo>[] attributes)
    {
        IEnumerable<IReadOnlyCollection<VariantAttributeValueInfo>> result = [[]];

        foreach (var attributeValues in attributes)
        {
            result = result.SelectMany(
                combination => attributeValues,
                (combination, value) => combination.Concat([value]).ToArray());
        }

        return result;
    }

    private static string BuildSku(string baseSku, IReadOnlyCollection<VariantAttributeValueInfo> values)
    {
        var suffix = string.Join("-", values.Select(value => NormalizeSku(value.ValueCode)));
        return string.IsNullOrWhiteSpace(suffix)
            ? NormalizeSku(baseSku)
            : $"{NormalizeSku(baseSku)}-{suffix}";
    }

    private static string BuildName(string productName, IReadOnlyCollection<VariantAttributeValueInfo> values)
    {
        var suffix = string.Join(" / ", values.Select(value => value.ValueName.Trim()));
        return string.IsNullOrWhiteSpace(suffix)
            ? productName.Trim()
            : $"{productName.Trim()} - {suffix}";
    }

    private static string NormalizeSku(string value) =>
        value.Trim().ToUpperInvariant().Replace(" ", "-", StringComparison.Ordinal);
}
