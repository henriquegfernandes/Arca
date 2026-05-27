using Arca.Domain.Enums;

namespace Arca.Domain.Entities;

public sealed class StockLocation : BaseEntity
{
    public Guid StoreId { get; private set; }
    public string Name { get; private set; }
    public string Type { get; private set; }
    public bool IsActive { get; private set; }

    public StockLocation(Guid storeId, string name, string type)
    {
        StoreId = storeId;
        Name = RequireText(name, nameof(name));
        Type = RequireText(type, nameof(type));
        IsActive = true;
    }

    private static string RequireText(string value, string name) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{name} is required.", name) : value.Trim();
}

public sealed class InventoryBalance
{
    public Guid Id { get; private set; }
    public Guid StockLocationId { get; private set; }
    public Guid ProductVariantId { get; private set; }
    public int Quantity { get; private set; }
    public int ReservedQuantity { get; private set; }
    public int MinimumStock { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public int AvailableQuantity => Quantity - ReservedQuantity;

    public InventoryBalance(Guid stockLocationId, Guid productVariantId)
    {
        Id = Guid.NewGuid();
        StockLocationId = stockLocationId;
        ProductVariantId = productVariantId;
    }

    public void ApplyDelta(int quantityDelta)
    {
        var newQuantity = Quantity + quantityDelta;
        if (newQuantity < 0)
        {
            throw new InvalidOperationException("Inventory balance cannot become negative.");
        }

        Quantity = newQuantity;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AdjustTo(int newQuantity, int? minimumStock = null)
    {
        if (newQuantity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(newQuantity), "Quantity cannot be negative.");
        }

        Quantity = newQuantity;
        if (minimumStock is not null)
        {
            MinimumStock = minimumStock.Value;
        }

        UpdatedAt = DateTime.UtcNow;
    }
}

public sealed class InventoryBatch : BaseEntity
{
    public Guid StockLocationId { get; private set; }
    public Guid ProductVariantId { get; private set; }
    public string? BatchNumber { get; private set; }
    public DateTime? ExpirationDate { get; private set; }
    public int Quantity { get; private set; }

    public InventoryBatch(Guid stockLocationId, Guid productVariantId, int quantity, string? batchNumber = null, DateTime? expirationDate = null)
    {
        if (quantity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Batch quantity cannot be negative.");
        }

        StockLocationId = stockLocationId;
        ProductVariantId = productVariantId;
        Quantity = quantity;
        BatchNumber = batchNumber;
        ExpirationDate = expirationDate;
    }
}

public sealed class StockMovement : BaseEntity
{
    public Guid TenantId { get; private set; }
    public Guid StoreId { get; private set; }
    public Guid StockLocationId { get; private set; }
    public Guid ProductVariantId { get; private set; }
    public StockMovementType Type { get; private set; }
    public int Quantity { get; private set; }
    public decimal? UnitCost { get; private set; }
    public string? Reason { get; private set; }
    public string? Notes { get; private set; }
    public Guid? UserId { get; private set; }

    public StockMovement(Guid tenantId, Guid storeId, Guid stockLocationId, Guid productVariantId, StockMovementType type, int quantity)
    {
        if (quantity == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Movement quantity cannot be zero.");
        }

        TenantId = tenantId;
        StoreId = storeId;
        StockLocationId = stockLocationId;
        ProductVariantId = productVariantId;
        Type = type;
        Quantity = quantity;
    }

    private static string RequireText(string value, string name) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{name} is required.", name) : value.Trim();
}
