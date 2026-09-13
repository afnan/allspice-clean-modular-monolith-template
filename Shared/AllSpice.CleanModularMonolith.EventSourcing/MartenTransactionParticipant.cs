using AllSpice.CleanModularMonolith.SharedKernel.EventSourcing;
using AllSpice.CleanModularMonolith.SharedKernel.Events;
using AllSpice.CleanModularMonolith.SharedKernel.Exceptions;
using AllSpice.CleanModularMonolith.SharedKernel.Persistence;
using JasperFx;
using Marten;
using Marten.Exceptions;
using Marten.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace AllSpice.CleanModularMonolith.EventSourcing;

/// <summary>
/// Scoped bridge between a module's Marten store and its EF Core <see cref="DbContext"/> transaction.
/// <para>
/// On first <see cref="GetSessionAsync"/> it ensures the owner context has a transaction (beginning one if
/// the handler got here before <c>TransactionBehavior</c>) and opens a Marten session enlisted in that same
/// Npgsql transaction with <c>shouldAutoCommit: false</c> — so <c>SaveChangesAsync</c> on the session writes
/// events and inline projections but never commits. Commit/rollback stay with <c>TransactionBehavior</c>,
/// which calls <see cref="FlushAsync"/> inside the transaction and <see cref="Discard"/> on failure.
/// </para>
/// </summary>
/// <typeparam name="TStore">The module's store marker (<c>ILedgerEventStore : IDocumentStore</c>).</typeparam>
/// <typeparam name="TContext">The module's DbContext (transaction owner, outbox host).</typeparam>
public sealed class MartenTransactionParticipant<TStore, TContext>(
    TStore store,
    TContext owner,
    IEventMetadataProvider metadata,
    ILogger<MartenTransactionParticipant<TStore, TContext>> logger)
    : AllSpice.CleanModularMonolith.SharedKernel.Persistence.ITransactionParticipant, IModuleEventStoreSession<TStore>, IAsyncDisposable
    where TStore : IDocumentStore
    where TContext : DbContext, IModuleDbContext
{
    private readonly TStore _store = store;
    private readonly TContext _owner = owner;
    private readonly IEventMetadataProvider _metadata = metadata;
    private readonly ILogger<MartenTransactionParticipant<TStore, TContext>> _logger = logger;
    private readonly List<IEventSourcedAggregate> _tracked = [];
    private IDocumentSession? _session;
    private bool _hasPendingChanges;

    public DbContext Owner => _owner;

    public bool HasPendingChanges => _hasPendingChanges;

    public async ValueTask<IDocumentSession> GetSessionAsync(CancellationToken cancellationToken)
    {
        if (_session is not null)
        {
            return _session;
        }

        // Open the module transaction early if TransactionBehavior hasn't yet — it will detect and reuse it.
        IDbContextTransaction transaction = _owner.Database.CurrentTransaction
            ?? await _owner.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        if (transaction.GetDbTransaction() is not NpgsqlTransaction npgsqlTransaction)
        {
            throw new InvalidOperationException(
                $"{typeof(TContext).Name} is not using Npgsql; the Marten event store can only enlist in a PostgreSQL transaction.");
        }

        _session = _store.LightweightSession(SessionOptions.ForTransaction(npgsqlTransaction, shouldAutoCommit: false));
        _session.CorrelationId = _metadata.CorrelationId;
        if (_metadata.IdempotencyKey is { Length: > 0 } idempotencyKey)
        {
            _session.SetHeader(EventHeaders.IdempotencyKey, idempotencyKey);
        }

        _logger.LogDebug("Opened {Store} session inside transaction {TransactionId}",
            typeof(TStore).Name, transaction.TransactionId);
        return _session;
    }

    public IQuerySession OpenQuerySession() => _store.QuerySession();

    public void Track(IEventSourcedAggregate aggregate)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        if (!_tracked.Any(a => ReferenceEquals(a, aggregate)))
        {
            _tracked.Add(aggregate);
        }
    }

    public void MarkPending() => _hasPendingChanges = true;

    public async ValueTask FlushAsync(CancellationToken cancellationToken)
    {
        if (_session is null || !_hasPendingChanges)
        {
            return;
        }

        if (_owner.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "Event store flush requested without an active module transaction. TransactionBehavior must own the transaction.");
        }

        try
        {
            await _session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ConcurrencyException ex)
        {
            // Event-stream expected-version conflict raised by JasperFx.Events.EventStreamUnexpectedMaxEventIdException
            // or JasperFx.Events.DcbConcurrencyException (both derive from JasperFx.ConcurrencyException): another
            // request appended to the same stream between our FetchForWriting and this flush.
            throw new ConcurrencyConflictException(
                "The aggregate was modified by another request after it was loaded. Reload and retry.", ex);
        }
        catch (ConcurrentUpdateException ex)
        {
            // Marten document optimistic-concurrency conflict (e.g. an inline projection document guarded by
            // UseOptimisticConcurrency) raised inside the same SaveChangesAsync call.
            throw new ConcurrencyConflictException(
                "The aggregate was modified by another request after it was loaded. Reload and retry.", ex);
        }
        catch (Marten.Exceptions.ExistingStreamIdCollisionException ex)
        {
            throw new ConflictException("Event stream", ex.Id);
        }
        catch (JasperFx.Events.ExistingStreamIdCollisionException ex)
        {
            throw new ConflictException("Event stream", ex.Id);
        }

        foreach (var aggregate in _tracked)
        {
            aggregate.ClearUncommittedEvents();
        }

        _hasPendingChanges = false;
    }

    public IEnumerable<IDomainEvent> TakeDomainEvents() =>
        _tracked.SelectMany(a => a.TakeDomainEvents()).ToList();

    public void Discard()
    {
        _tracked.Clear();
        _hasPendingChanges = false;
        _session?.Dispose();
        _session = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_session is not null)
        {
            await _session.DisposeAsync().ConfigureAwait(false);
            _session = null;
        }
    }
}
