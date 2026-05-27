namespace Arca.Domain.Entities;

public sealed class Permission
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public string Module { get; private set; }

    public Permission(string name, string module, string? description = null)
    {
        Id = Guid.NewGuid();
        Name = RequireText(name, nameof(name));
        Module = RequireText(module, nameof(module));
        Description = description;
    }

    private static string RequireText(string value, string name) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{name} is required.", name) : value.Trim();
}
