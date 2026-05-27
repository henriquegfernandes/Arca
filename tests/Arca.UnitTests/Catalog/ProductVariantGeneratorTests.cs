using Arca.Application.Catalog;

namespace Arca.UnitTests.Catalog;

public sealed class ProductVariantGeneratorTests
{
    [Fact]
    public void GenerateVariants_CreatesCartesianSkuAndNameCombinations()
    {
        var colorAttributeId = Guid.NewGuid();
        var sizeAttributeId = Guid.NewGuid();
        var generator = new ProductVariantGenerator();

        var variants = generator.GenerateVariants(new ProductVariantGenerationInput(
            ProductName: "Basic Shirt",
            BaseSku: "bas shirt",
            DefaultSalePrice: 79.90m,
            DefaultCostPrice: 35.50m,
            Status: "Active",
            SelectedAttributes:
            [
                [
                    new(colorAttributeId, "Color", 1, Guid.NewGuid(), "Black", "pre", 1),
                    new(colorAttributeId, "Color", 1, Guid.NewGuid(), "White", "bra", 2)
                ],
                [
                    new(sizeAttributeId, "Size", 2, Guid.NewGuid(), "P", "p", 1),
                    new(sizeAttributeId, "Size", 2, Guid.NewGuid(), "M", "m", 2)
                ]
            ],
            ExistingSkus: new HashSet<string>(StringComparer.OrdinalIgnoreCase)));

        Assert.Collection(
            variants,
            first =>
            {
                Assert.Equal("BAS-SHIRT-PRE-P", first.Sku);
                Assert.Equal("Basic Shirt - Black / P", first.Name);
            },
            second => Assert.Equal("BAS-SHIRT-PRE-M", second.Sku),
            third => Assert.Equal("BAS-SHIRT-BRA-P", third.Sku),
            fourth => Assert.Equal("BAS-SHIRT-BRA-M", fourth.Sku));
    }

    [Fact]
    public void GenerateVariants_FiltersExistingAndDuplicateSkus()
    {
        var colorAttributeId = Guid.NewGuid();
        var generator = new ProductVariantGenerator();

        var variants = generator.GenerateVariants(new ProductVariantGenerationInput(
            ProductName: "Cap",
            BaseSku: "CAP",
            DefaultSalePrice: 49m,
            DefaultCostPrice: null,
            Status: "Active",
            SelectedAttributes:
            [
                [
                    new(colorAttributeId, "Color", 1, Guid.NewGuid(), "Black", "PRE", 1),
                    new(colorAttributeId, "Color", 1, Guid.NewGuid(), "Duplicated Black", "PRE", 2),
                    new(colorAttributeId, "Color", 1, Guid.NewGuid(), "White", "BRA", 3)
                ]
            ],
            ExistingSkus: new HashSet<string>(["CAP-BRA"], StringComparer.OrdinalIgnoreCase)));

        var variant = Assert.Single(variants);
        Assert.Equal("CAP-PRE", variant.Sku);
    }
}
