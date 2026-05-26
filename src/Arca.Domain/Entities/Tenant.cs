namespace Arca.Domain.Entities;

public sealed class Tenant : BaseEntity
{
    public string Name { get; private set; }
    public string Slug { get; private set; }
    public bool IsActive { get; private set; }
    public string SetupStatus { get; private set; }

    public Tenant(string name, string slug)
    {
        Name = name;
        Slug = slug;
        IsActive = true;
        SetupStatus = "Pending";
    }
}
