namespace Arca.Domain.Entities;

public sealed class Tenant : BaseEntity
{
    public string Name { get; private set; }
    public string? LegalName { get; private set; }
    public string? Document { get; private set; }
    public string Slug { get; private set; }
    public string? ContactEmail { get; private set; }
    public string? Phone { get; private set; }
    public string? MainSegment { get; private set; }
    public bool IsActive { get; private set; }
    public string SetupStatus { get; private set; }

    public Tenant(
        string name,
        string slug,
        string? legalName = null,
        string? document = null,
        string? contactEmail = null,
        string? phone = null,
        string? mainSegment = null)
    {
        Name = RequireText(name, nameof(name));
        Slug = RequireText(slug, nameof(slug)).ToLowerInvariant();
        LegalName = legalName;
        Document = document;
        ContactEmail = contactEmail;
        Phone = phone;
        MainSegment = mainSegment;
        IsActive = true;
        SetupStatus = "Pending";
    }

    public void CompleteSetup()
    {
        SetupStatus = "Completed";
        MarkUpdated();
    }

    public void Disable()
    {
        IsActive = false;
        MarkUpdated();
    }

    private static string RequireText(string value, string name) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{name} is required.", name) : value.Trim();
}
