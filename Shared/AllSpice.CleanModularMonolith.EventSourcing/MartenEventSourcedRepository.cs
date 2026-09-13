using AllSpice.CleanModularMonolith.SharedKernel.EventSourcing;
using AllSpice.CleanModularMonolith.SharedKernel.Events;
using Ardalis.GuardClauses;
using JasperFx.Events;
using Marten;

namespace AllSpice.CleanModularMonolith.EventSourcing;

/// <summary>
/// Marten-backed base for bespoke event-sourced repositories
/// (<c>AccountRepository : MartenEventSourcedRepository&lt;Account, ILedgerEventStore&gt;, IAccountRepository</c>).
/// <para>
/// <see cref="LoadAsync"/> uses <c>FetchForWriting</c>, which records the stream's current version so a
/// concurrent append is detected at flush (→ <c>ConcurrencyConflictException</c>). Unregistered aggregate
/// types are built by Marten's <i>live aggregation</i> — replaying the stream through the aggregate's
/// <c>Apply(TEvent)</c> methods — so the write model needs no projection registration. The stream version
/// is stamped from <c>IEventStream.CurrentVersion</c>, not from a property convention.
/// </para>
/// </summary>
public abstract class MartenEventSourcedRepository<TAggregate, TStore>(IModuleEventStoreSession<TStore> session)
    : IEventSourcedRepository<TAggregate>
    where TAggregate : EventSourcedAggregate
    where TStore : IDocumentStore
{
    private readonly IModuleEventStoreSession<TStore> _session = session;
    private readonly Dictionary<Guid, IEventStream<TAggregate>> _streams = [];

    /// <summary>For projection queries in derived repositories (<c>Session.OpenQuerySession()</c>).</summary>
    protected IModuleEventStoreSession<TStore> Session => _session;

    public async Task<TAggregate?> LoadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Guard.Against.Default(id);

        var documentSession = await _session.GetSessionAsync(cancellationToken).ConfigureAwait(false);
        var stream = await documentSession.Events.FetchForWriting<TAggregate>(id, cancellationToken).ConfigureAwait(false);

        if (stream.Aggregate is null)
        {
            return null;
        }

        _streams[id] = stream;
        IEventSourcedAggregate aggregate = stream.Aggregate;
        aggregate.SetVersion(stream.CurrentVersion ?? 0);
        _session.Track(aggregate);
        return stream.Aggregate;
    }

    public async Task AddAsync(TAggregate aggregate, CancellationToken cancellationToken = default)
    {
        Guard.Against.Null(aggregate);
        Guard.Against.Default(aggregate.Id, message: "An event-sourced aggregate must set its Id in the first Apply.");
        if (aggregate.UncommittedEvents.Count == 0)
        {
            throw new InvalidOperationException($"{typeof(TAggregate).Name} has no events to start a stream with.");
        }

        var documentSession = await _session.GetSessionAsync(cancellationToken).ConfigureAwait(false);
        documentSession.Events.StartStream<TAggregate>(aggregate.Id, aggregate.UncommittedEvents.Cast<object>().ToArray());
        _session.Track(aggregate);
        _session.MarkPending();
    }

    public async Task SaveAsync(TAggregate aggregate, CancellationToken cancellationToken = default)
    {
        Guard.Against.Null(aggregate);

        if (!_streams.TryGetValue(aggregate.Id, out var stream))
        {
            throw new InvalidOperationException(
                $"{typeof(TAggregate).Name} {aggregate.Id} was not loaded through LoadAsync in this scope. " +
                "Load before saving; use AddAsync for a new aggregate.");
        }

        var documentSession = await _session.GetSessionAsync(cancellationToken).ConfigureAwait(false);

        if (aggregate.UncommittedEvents.Count > 0)
        {
            stream.AppendMany(aggregate.UncommittedEvents.Cast<object>().ToArray());
        }

        if (aggregate.IsMarkedForArchive)
        {
            documentSession.Events.ArchiveStream(aggregate.Id);
        }

        _session.Track(aggregate);
        _session.MarkPending();
    }

    public async Task<IReadOnlyList<StoredEvent>> HistoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Guard.Against.Default(id);

        await using var query = _session.OpenQuerySession();
        var events = await query.Events.FetchStreamAsync(id, token: cancellationToken).ConfigureAwait(false);

        return events
            .Select(e => new StoredEvent(
                e.Version,
                e.Sequence,
                e.Timestamp,
                e.EventTypeName,
                e.CorrelationId,
                e.Headers is null
                    ? new Dictionary<string, object?>()
                    : e.Headers.ToDictionary(kv => kv.Key, kv => (object?)kv.Value),
                (IDomainEvent)e.Data))
            .ToList();
    }
}
