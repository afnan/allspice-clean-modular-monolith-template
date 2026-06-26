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
/// Generic entity base using strongly typed identifiers. Identity-based equality: two entities are equal only
/// when they are the same runtime type and share the same <see cref="Id"/>.
/// </summary>
public abstract class Entity<TId> : HasDomainEventsBase
    where TId : IEquatable<TId>
{
    public TId Id { get; protected set; } = default!;

    protected void RegisterDomainEvent(IDomainEvent domainEvent) => AddDomainEvent(domainEvent);

    /// <summary>
    /// True while the entity has no real identity yet (its <see cref="Id"/> is still the default — e.g. an
    /// unsaved entity with a DB-generated key). Transient entities are never equal to one another, since each
    /// freshly-constructed entity is a distinct identity until persistence assigns a key.
    /// </summary>
    private bool IsTransient() => EqualityComparer<TId>.Default.Equals(Id, default!);

    public override bool Equals(object? obj)
    {
        // Compare the runtime type too: two different entity types that happen to share an Id are NOT equal,
        // otherwise they would collide in a HashSet / LINQ Distinct.
        if (obj is not Entity<TId> other || GetType() != other.GetType())
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        // Two transient entities are distinct identities even if both Ids are still default.
        if (IsTransient() || other.IsTransient())
        {
            return false;
        }

        return EqualityComparer<TId>.Default.Equals(Id, other.Id);
    }

    public static bool operator ==(Entity<TId>? left, Entity<TId>? right) => Equals(left, right);

    public static bool operator !=(Entity<TId>? left, Entity<TId>? right) => !(left == right);

    // Derived from the current Id, NOT cached: a hash cached while the Id was still transient would go stale
    // once a DB-generated key is assigned, "losing" the entity in a hash-based collection. (Standard caveat:
    // don't rely on an unsaved entity's hash across the point its key is assigned.)
    public override int GetHashCode() => EqualityComparer<TId>.Default.GetHashCode(Id);
}
