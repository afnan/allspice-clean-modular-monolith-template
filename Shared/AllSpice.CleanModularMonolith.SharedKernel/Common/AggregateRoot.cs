namespace AllSpice.CleanModularMonolith.SharedKernel.Common;

/// <summary>
/// Aggregate root base class with domain event support.
/// </summary>
public abstract class AggregateRoot : Entity
{
}

public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : IEquatable<TId>
{
}


