namespace Arca.Domain.Entities;

public sealed class TenantSettings : BaseEntity
{
    public Guid TenantId { get; private set; }
    public string Currency { get; private set; }
    public string TimeZone { get; private set; }
    public string DefaultLanguage { get; private set; }
    public bool AllowMultipleStores { get; private set; }
    public bool AllowBatchControl { get; private set; }
    public bool AllowExpirationControl { get; private set; }
    public bool AllowStoreSpecificPricing { get; private set; }

    public TenantSettings(
        Guid tenantId,
        string currency,
        string timeZone,
        string defaultLanguage,
        bool allowMultipleStores,
        bool allowBatchControl,
        bool allowExpirationControl,
        bool allowStoreSpecificPricing)
    {
        TenantId = tenantId;
        Currency = RequireText(currency, nameof(currency));
        TimeZone = RequireText(timeZone, nameof(timeZone));
        DefaultLanguage = RequireText(defaultLanguage, nameof(defaultLanguage));
        AllowMultipleStores = allowMultipleStores;
        AllowBatchControl = allowBatchControl;
        AllowExpirationControl = allowExpirationControl;
        AllowStoreSpecificPricing = allowStoreSpecificPricing;
    }

    private static string RequireText(string value, string name) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{name} is required.", name) : value.Trim();
}
