namespace Arca.Domain.Entities;

public sealed class Store : BaseEntity
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; }
    public string Code { get; private set; }
    public string? Document { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? AddressLine { get; private set; }
    public string? City { get; private set; }
    public string? State { get; private set; }
    public string? ZipCode { get; private set; }
    public string Type { get; private set; }
    public bool IsActive { get; private set; }

    public Store(Guid tenantId, string name, string code, string type)
    {
        TenantId = tenantId;
        Name = RequireText(name, nameof(name));
        Code = RequireText(code, nameof(code)).ToUpperInvariant();
        Type = RequireText(type, nameof(type));
        IsActive = true;
    }

    public void Disable()
    {
        IsActive = false;
        MarkUpdated();
    }

    private static string RequireText(string value, string name) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{name} is required.", name) : value.Trim();
}
