namespace Arca.Application.Tenancy;

public sealed class CatalogTemplateSeeder
{
    public CatalogTemplateDefinition Build(string template)
    {
        return Normalize(template) switch
        {
            "fashion" => BuildFashion(),
            "shoes" => BuildShoes(),
            "electronics" => BuildElectronics(),
            "religiousgoods" => BuildReligiousGoods(),
            "foodbakery" => BuildFoodBakery(),
            "snackbarrestaurant" => BuildSnackBarRestaurant(),
            "market" => BuildMarket(),
            "custom" => Empty("Custom"),
            _ => Empty("Custom")
        };
    }

    public static bool IsKnownTemplate(string template)
    {
        var normalized = Normalize(template);
        return normalized is "fashion" or "shoes" or "electronics" or "religiousgoods"
            or "foodbakery" or "snackbarrestaurant" or "market" or "custom";
    }

    private static CatalogTemplateDefinition BuildFashion()
    {
        var color = Attribute("Color", "COR", "Select", true, true, 1,
        [
            Value("Preto", "PRE", "#111827", 1),
            Value("Branco", "BRA", "#FFFFFF", 2),
            Value("Cinza", "CIN", "#6B7280", 3)
        ]);

        var size = Attribute("Size", "TAM", "Select", true, true, 2,
        [
            Value("P", "P", null, 1),
            Value("M", "M", null, 2),
            Value("G", "G", null, 3),
            Value("GG", "GG", null, 4)
        ]);

        var model = Attribute("Model", "MOD", "Select", true, false, 3,
        [
            Value("Regular", "REG", null, 1),
            Value("Oversized", "OVER", null, 2)
        ]);

        return new CatalogTemplateDefinition(
            "Fashion",
            [
                new("Clothing", "clothing", "General clothing items.", 1),
                new("Shoes", "shoes", "Footwear and related items.", 2),
                new("Accessories", "accessories", "Caps, bags and accessories.", 3)
            ],
            [color, size, model, TextAttribute("Brand", "BRAND", 4)],
            [
                ProductType("Apparel", "Generic apparel product type.", ["COR", "TAM", "MOD"]),
                ProductType("Accessory", "Accessories with optional color variation.", ["COR"])
            ]);
    }

    private static CatalogTemplateDefinition BuildShoes() =>
        new(
            "Shoes",
            [
                new("Shoes", "shoes", "Footwear catalog.", 1),
                new("Care", "care", "Cleaning and care products.", 2)
            ],
            [
                Attribute("Size", "SIZE", "Select", true, true, 1, [Value("38", "38", null, 1), Value("39", "39", null, 2), Value("40", "40", null, 3), Value("41", "41", null, 4)]),
                Attribute("Color", "COLOR", "Select", true, true, 2, [Value("Black", "BLK", "#111827", 1), Value("White", "WHT", "#FFFFFF", 2)]),
                TextAttribute("Material", "MAT", 3)
            ],
            [ProductType("Footwear", "Shoes and sandals.", ["SIZE", "COLOR"])]);

    private static CatalogTemplateDefinition BuildElectronics() =>
        new(
            "Electronics",
            [new("Devices", "devices", "Electronic devices.", 1), new("Accessories", "accessories", "Electronic accessories.", 2)],
            [
                TextAttribute("Brand", "BRAND", 1),
                Attribute("Voltage", "VOLT", "Select", true, false, 2, [Value("110V", "110", null, 1), Value("220V", "220", null, 2), Value("Bivolt", "BIV", null, 3)]),
                TextAttribute("Warranty", "WARR", 3)
            ],
            [ProductType("Electronic Product", "Generic electronics product type.", ["VOLT"])]);

    private static CatalogTemplateDefinition BuildReligiousGoods() =>
        new(
            "ReligiousGoods",
            [new("Images", "images", "Images and statues.", 1), new("Books", "books", "Books and devotionals.", 2), new("Gifts", "gifts", "Religious gifts.", 3)],
            [TextAttribute("Material", "MAT", 1), Attribute("Size", "SIZE", "Select", true, false, 2, [Value("Small", "S", null, 1), Value("Medium", "M", null, 2), Value("Large", "L", null, 3)])],
            [ProductType("Religious Item", "Generic religious goods product type.", ["SIZE"])]);

    private static CatalogTemplateDefinition BuildFoodBakery() =>
        new(
            "FoodBakery",
            [new("Breads", "breads", "Breads and bakery items.", 1), new("Cakes", "cakes", "Cakes and desserts.", 2), new("Drinks", "drinks", "Beverages.", 3)],
            [
                Attribute("Unit", "UNIT", "Select", false, true, 1, [Value("Unit", "UN", null, 1), Value("Kg", "KG", null, 2)]),
                Attribute("Flavor", "FLAV", "Select", true, false, 2, [Value("Chocolate", "CHO", null, 1), Value("Vanilla", "VAN", null, 2)])
            ],
            [ProductType("Prepared Food", "Bakery and prepared food product type.", ["FLAV"])]);

    private static CatalogTemplateDefinition BuildSnackBarRestaurant() =>
        new(
            "SnackBarRestaurant",
            [new("Snacks", "snacks", "Snacks and meals.", 1), new("Combos", "combos", "Combos.", 2), new("Drinks", "drinks", "Beverages.", 3)],
            [
                Attribute("Size", "SIZE", "Select", true, false, 1, [Value("Small", "S", null, 1), Value("Medium", "M", null, 2), Value("Large", "L", null, 3)]),
                Attribute("Option", "OPT", "Select", true, false, 2, [Value("Traditional", "TRA", null, 1), Value("Special", "ESP", null, 2)])
            ],
            [ProductType("Menu Item", "Restaurant menu item.", ["SIZE", "OPT"])]);

    private static CatalogTemplateDefinition BuildMarket() =>
        new(
            "Market",
            [new("Groceries", "groceries", "Grocery products.", 1), new("Cleaning", "cleaning", "Cleaning products.", 2), new("Personal Care", "personal-care", "Personal care products.", 3)],
            [
                TextAttribute("Brand", "BRAND", 1),
                Attribute("Package", "PACK", "Select", true, false, 2, [Value("Unit", "UN", null, 1), Value("Pack", "PACK", null, 2), Value("Box", "BOX", null, 3)])
            ],
            [ProductType("Market Product", "General market product type.", ["PACK"])]);

    private static CatalogTemplateDefinition Empty(string name) => new(name, [], [], []);

    private static ProductAttributeSeed TextAttribute(string name, string code, int sortOrder) =>
        Attribute(name, code, "Text", false, false, sortOrder, []);

    private static ProductAttributeSeed Attribute(
        string name,
        string code,
        string type,
        bool variant,
        bool required,
        int sortOrder,
        IReadOnlyCollection<ProductAttributeValueSeed> values) =>
        new(name, code, type, variant, required, sortOrder, values);

    private static ProductAttributeValueSeed Value(string name, string code, string? hexCode, int sortOrder) =>
        new(name, code, name, hexCode, sortOrder);

    private static ProductTypeSeed ProductType(string name, string description, IReadOnlyCollection<string> attributeCodes) =>
        new(name, description, attributeCodes.Select((code, index) => new ProductTypeAttributeSeed(code, true, true, index + 1)).ToArray());

    private static string Normalize(string value) =>
        value.Trim().Replace("-", string.Empty, StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
}
