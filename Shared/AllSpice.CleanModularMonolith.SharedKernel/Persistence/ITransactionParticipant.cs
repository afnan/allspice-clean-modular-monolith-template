using AllSpice.CleanModularMonolith.SharedKernel.Events;
using Microsoft.EntityFrameworkCore;

namespace AllSpice.CleanModularMonolith.SharedKernel.Persistence;

/// <summary>
/// A scoped unit-of-work participant that joins the transaction of a module <see cref="DbContext"/> — the
/// seam through which an event store (Marten, see <c>Shared/*.EventSourcing</c>) commits atomically with EF
/// writes and the co-located Wolverine outbox. <c>TransactionBehavior</c> treats a participant's
/// <see cref="Owner"/> as the module it mutates (one module per command still applies), flushes it inside the
/// owner's transaction on every drain-loop iteration, collects its domain events, and discards it on failure.
/// </summary>
public interface ITransactionParticipant
{
    /// <summary>The module DbContext whose transaction this participant enlists in.</summary>
    DbContext Owner { get; }

    /// <summary>True when the participant has staged work (appends, archives) that has not been flushed.</summary>
    bool HasPendingChanges { get; }

    /// <summary>
    /// Persists staged work inside <see cref="Owner"/>'s current transaction. Must never commit — the behavior
    /// owns commit/rollback. Called only while <c>Owner.Database.CurrentTransaction</c> is non-null.
    /// </summary>
    ValueTask FlushAsync(CancellationToken cancellationToken);

    /// <summary>Drains domain events from aggregates loaded or added through this participant.</summary>
    IEnumerable<IDomainEvent> TakeDomainEvents();

    /// <summary>Drops staged work and tracked aggregates (failure Result, exception, or rollback).</summary>
    void Discard();
}
