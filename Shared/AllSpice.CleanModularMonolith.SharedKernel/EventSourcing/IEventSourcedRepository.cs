namespace AllSpice.CleanModularMonolith.SharedKernel.EventSourcing;

/// <summary>
/// Persistence contract for an event-sourced aggregate. Bespoke repositories extend it
/// (<c>IAccountRepository : IEventSourcedRepository&lt;Account&gt;</c>) — golden rule 4 applies unchanged.
/// <para>
/// <see cref="LoadAsync"/> declares write intent: it opens the module transaction if needed and tracks the
/// stream's version for optimistic concurrency. Use a projection query (not the stream) for reads.
/// </para>
/// </summary>
public interface IEventSourcedRepository<TAggregate>
    where TAggregate : EventSourcedAggregate
{
    /// <summary>Loads the current state for writing, or <c>null</c> when the stream does not exist.</summary>
    Task<TAggregate?> LoadAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Starts a new stream from the aggregate's uncommitted events.</summary>
    Task AddAsync(TAggregate aggregate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends the aggregate's uncommitted events to a stream previously obtained via <see cref="LoadAsync"/>,
    /// with the loaded version as the expected version; archives the stream if requested.
    /// </summary>
    Task SaveAsync(TAggregate aggregate, CancellationToken cancellationToken = default);

    /// <summary>Returns the full stream with metadata (audit trail). Read-only; no transaction.</summary>
    Task<IReadOnlyList<StoredEvent>> HistoryAsync(Guid id, CancellationToken cancellationToken = default);
}
