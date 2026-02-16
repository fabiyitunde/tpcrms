namespace CRMS.Domain.Common;

public abstract class Entity
{
    public Guid Id { get; protected set; }
    public DateTime CreatedAt { get; protected set; }
    public string CreatedBy { get; protected set; } = string.Empty;
    public DateTime? ModifiedAt { get; protected set; }
    public string? ModifiedBy { get; protected set; }

    protected Entity()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
    }

    protected Entity(Guid id)
    {
        Id = id;
        CreatedAt = DateTime.UtcNow;
    }

    public void SetAuditInfo(string userId, bool isNew = false)
    {
        if (isNew)
        {
            CreatedBy = userId;
            CreatedAt = DateTime.UtcNow;
        }
        else
        {
            ModifiedBy = userId;
            ModifiedAt = DateTime.UtcNow;
        }
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Entity other)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (GetType() != other.GetType())
            return false;

        return Id == other.Id;
    }

    public override int GetHashCode() => Id.GetHashCode();

    public static bool operator ==(Entity? left, Entity? right)
    {
        if (left is null && right is null)
            return true;

        if (left is null || right is null)
            return false;

        return left.Equals(right);
    }

    public static bool operator !=(Entity? left, Entity? right) => !(left == right);
}
