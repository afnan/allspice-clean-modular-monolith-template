using AllSpice.CleanModularMonolith.SharedKernel.Events;

namespace AllSpice.CleanModularMonolith.SharedKernel.Common;

/// <summary>
/// Base entity with <see cref="Guid"/> identifier.
/// </summary>
public abstract class Entity : Entity<Guid>
{
    protected Entity()
    {
        Id = Guid.NewGuid();
    }
}

/// <summary>
/// Generic entity base using strongly typed identifiers.
/// </summary>
public abstract class Entity<TId> : HasDomainEventsBase
    where TId : IEquatable<TId>
{
    private int? _requestedHashCode;

    public TId Id { get; protected set; } = default!;

    protected void RegisterDomainEvent(IDomainEvent domainEvent) => AddDomainEvent(domainEvent);

    public override bool Equals(object? obj)
    {
        if (obj is not Entity<TId> other)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return EqualityComparer<TId>.Default.Equals(Id, other.Id);
    }

    public static bool operator ==(Entity<TId>? left, Entity<TId>? right) => Equals(left, right);

    public static bool operator !=(Entity<TId>? left, Entity<TId>? right) => !(left == right);

    public override int GetHashCode()
    {
        if (!_requestedHashCode.HasValue)
        {
            _requestedHashCode = EqualityComparer<TId>.Default.GetHashCode(Id) ^ 31;
        }

        return _requestedHashCode.Value;
    }
}

/// <summary>
/// Strongly typed entity base that works with Vogen or similar libraries.
/// </summary>
public abstract class Entity<T, TId> : HasDomainEventsBase
    where T : Entity<T, TId>
{
    public TId Id { get; protected set; } = default!;
}


