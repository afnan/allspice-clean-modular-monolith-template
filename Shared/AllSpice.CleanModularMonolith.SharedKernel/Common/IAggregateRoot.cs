namespace AllSpice.CleanModularMonolith.SharedKernel.Common;

/// <summary>
/// Marks an entity as an aggregate root — the consistency and transaction boundary that repositories load
/// and save as a unit. It is a pure marker (no members): being a root is orthogonal to being auditable,
/// soft-deletable, etc., so any <see cref="Entity"/>/<see cref="AuditableEntity"/> can opt in by implementing
/// it. Repository abstractions constrain on this interface so only roots are persisted directly.
/// </summary>
public interface IAggregateRoot
{
}
