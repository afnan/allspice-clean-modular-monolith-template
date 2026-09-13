using AllSpice.CleanModularMonolith.SharedKernel.EventSourcing;
using AllSpice.CleanModularMonolith.SharedKernel.Events;
using Ardalis.GuardClauses;
using JasperFx.Events;
using Marten;
using Marten.Events.Archiving;

namespace AllSpice.CleanModularMonolith.EventSourcing;

/// <summary>
/// Marten-backed base for bespoke event-sourced repositories
/// (<c>AccountRepository : MartenEventSourcedRepository&lt;Account, ILedgerEventStore&gt;, IAccountRepository</c>).
/// <para>
/// <see cref="LoadAsync"/> uses <c>FetchForWriting</c>, which records the stream's current version so a
/// concurrent append is detected at flush (→ <c>ConcurrencyConflictException</c>). <c>FetchForWriting&lt;T&gt;</c>
/// resolves and builds <typeparamref name="TAggregate"/> at read time via Marten's <i>live aggregation</i> —
/// replaying the stream through the aggregate's <c>Apply(TEvent)</c> methods — with no projection
/// registration needed for that resolution to work. The store's <b>schema migration</b> is a separate
/// concern: an event type or projection (<c>opts.Projections.LiveStreamAggregation&lt;TAggregate&gt;()</c> is
/// the natural form, one per event-sourced aggregate) must still be registered in the module's
/// <c>AddModuleEventStore</c> <c>configure</c> callback, or <c>ApplyEventStoreSchemaAsync</c> creates no
/// <c>mt_events</c>/<c>mt_streams</c> tables at all — see that method's guard. The stream version is stamped
/// from <c>IEventStream.CurrentVersion</c>, not from a property convention.
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

    /// <summary>
    /// Reads the stream on its own query session, outside the module transaction — appends staged by
    /// <see cref="AddAsync"/>/<see cref="SaveAsync"/> in the same scope but not yet flushed are not visible.
    /// <para>
    /// This is the <b>audit trail</b>, so it deliberately INCLUDES archived streams. Marten's
    /// <c>FetchStreamAsync</c> filters archived events out, which would make history vanish for exactly the
    /// aggregates an auditor cares about (a closed account), so the raw event LINQ with
    /// <c>MaybeArchived()</c> is used instead.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<StoredEvent>> HistoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Guard.Against.Default(id);

        await using var query = _session.OpenQuerySession();
        var events = await query.Events.QueryAllRawEvents()
            .Where(e => e.StreamId == id && e.MaybeArchived())
            .OrderBy(e => e.Version)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

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
