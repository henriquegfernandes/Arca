namespace Arca.Domain.Entities;

public sealed class User : BaseEntity
{
    public string FullName { get; private set; }
    public string Email { get; private set; }
    public string NormalizedEmail { get; private set; }
    public string? Phone { get; private set; }
    public string PasswordHash { get; private set; }
    public bool IsActive { get; private set; }
    public bool EmailConfirmed { get; private set; }
    public DateTime? LastLoginAt { get; private set; }

    public User(string fullName, string email, string passwordHash, string? phone = null)
    {
        FullName = RequireText(fullName, nameof(fullName));
        Email = RequireText(email, nameof(email));
        NormalizedEmail = Email.ToUpperInvariant();
        Phone = phone;
        PasswordHash = RequireText(passwordHash, nameof(passwordHash));
        IsActive = true;
        EmailConfirmed = false;
    }

    public void ConfirmEmail()
    {
        EmailConfirmed = true;
        MarkUpdated();
    }

    public void RegisterLogin(DateTime when)
    {
        LastLoginAt = when;
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
