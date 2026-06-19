namespace Arca.Domain.Entities;

public abstract class BaseEntity
{
    public Guid Id { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    protected BaseEntity()
        : this(Guid.NewGuid(), DateTime.UtcNow)
    {
    }

    protected BaseEntity(Guid id, DateTime createdAt)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        CreatedAt = createdAt;
    }

    protected void MarkUpdated() => UpdatedAt = DateTime.UtcNow;
}
