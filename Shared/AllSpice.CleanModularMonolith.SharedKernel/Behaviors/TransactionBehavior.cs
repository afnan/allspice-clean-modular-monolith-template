using AllSpice.CleanModularMonolith.SharedKernel.Events;
using AllSpice.CleanModularMonolith.SharedKernel.Exceptions;
using AllSpice.CleanModularMonolith.SharedKernel.Messaging;
using AllSpice.CleanModularMonolith.SharedKernel.Persistence;
using Ardalis.Result;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace AllSpice.CleanModularMonolith.SharedKernel.Behaviors;

/// <summary>
/// Pipeline behavior that owns the unit-of-work boundary for <see cref="ITransactional"/> commands.
/// <para>
/// Repositories stage writes only (see <c>EfRepository.SaveChangesAsync</c>), so the handler performs no
/// database writes itself. After the handler returns, this behavior finds the single dirty module
/// <see cref="IModuleDbContext"/>, opens a transaction <b>on that context</b> (or reuses one an
/// <see cref="ITransactionParticipant"/> opened early), flushes the staged writes and the participants,
/// drains domain events (which may stage more and may publish integration events that enrol the same
/// transaction's outbox), then commits — or rolls everything back on any failure.
/// </para>
/// <para>
/// <b>Participants.</b> An event store (Marten) cannot stage-only: its write API needs a live session during
/// the handler, and that session must sit inside the module transaction to be atomic with the outbox. So the
/// module transaction is opened <i>lazily</i> — here at commit time, or earlier by a participant on first
/// write-intent. Either way this behavior is the only thing that commits or rolls back. A participant's
/// <see cref="ITransactionParticipant.Owner"/> counts as the module it mutates (one module per command).
/// </para>
/// </summary>
public sealed class TransactionBehavior<TRequest, TResponse>(
    IEnumerable<IModuleDbContext> dbContexts,
    IEnumerable<ITransactionParticipant> participants,
    IDomainEventDispatcher dispatcher,
    IEnumerable<IOutboxFlusher> outboxFlushers,
    IPostCommitActions postCommitActions,
    ILogger<TransactionBehavior<TRequest, TResponse>> logger) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : class, IMessage, ITransactional
{
    private readonly IEnumerable<IModuleDbContext> _dbContexts = dbContexts;
    private readonly IReadOnlyList<ITransactionParticipant> _participants = participants.ToList();
    private readonly IDomainEventDispatcher _dispatcher = dispatcher;
    private readonly IEnumerable<IOutboxFlusher> _outboxFlushers = outboxFlushers;
    private readonly IPostCommitActions _postCommitActions = postCommitActions;
    private readonly ILogger<TransactionBehavior<TRequest, TResponse>> _logger = logger;

    public async ValueTask<TResponse> Handle(
        TRequest request,
        MessageHandlerDelegate<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        TResponse response;
        try
        {
            // Repositories stage only, so the handler performs no DB writes itself — EXCEPT that an event-store
            // participant may have opened the module transaction early. If the handler throws, release that
            // transaction (nothing was flushed) and drop the participant's staged work.
            response = await next(request, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await ReleaseOpenTransactionsAsync(discardParticipants: true).ConfigureAwait(false);
            throw;
        }

        // Exactly one module may be dirty: a context with tracked changes, or the owner of a participant with
        // pending work. Cross-module side effects must go through integration events (Wolverine outbox).
        var dirtyContexts = _dbContexts
            .Select(c => c.Instance)
            .Where(db => db.ChangeTracker.HasChanges() || _participants.Any(p => p.HasPendingChanges && ReferenceEquals(p.Owner, db)))
            .Distinct(ReferenceEqualityComparer.Instance)
            .Cast<DbContext>()
            .ToList();

        if (dirtyContexts.Count == 0)
        {
            // Nothing to commit. A participant may still hold an early transaction (it loaded but raised nothing)
            // — release it so the connection isn't left inside an open transaction for the rest of the scope.
            await ReleaseOpenTransactionsAsync(discardParticipants: true).ConfigureAwait(false);
            return response;
        }

        // Failure must not mutate state. If the handler staged writes but signalled failure by RETURNING a
        // failure Result (instead of throwing), discard the staged changes rather than committing them.
        // Success is ResultStatus.Ok here (Created/NoContent are applied at the endpoint layer).
        if (response is IResult result &&
            result.Status is not (ResultStatus.Ok or ResultStatus.Created or ResultStatus.NoContent))
        {
            _logger.LogWarning(
                "{RequestType} returned a failure Result ({Status}) after staging writes to {DbContext}; " +
                "discarding the staged changes without committing.",
                typeof(TRequest).Name,
                result.Status,
                dirtyContexts[0].GetType().Name);

            // Clearing the change tracker is not optional: the module DbContext is scoped to the request, so
            // staged entities stay tracked after we return and a SUBSEQUENT ITransactional command in the same
            // scope would commit them. Participants are discarded and any early transaction rolled back.
            foreach (var dirty in dirtyContexts)
            {
                dirty.ChangeTracker.Clear();
            }

            await ReleaseOpenTransactionsAsync(discardParticipants: true).ConfigureAwait(false);
            return response;
        }

        if (dirtyContexts.Count > 1)
        {
            var contextNames = string.Join(", ", dirtyContexts.Select(c => c.GetType().Name));
            await ReleaseOpenTransactionsAsync(discardParticipants: true).ConfigureAwait(false);
            throw new InvalidOperationException(
                $"{typeof(TRequest).Name} mutated multiple module DbContexts ({contextNames}). " +
                "A command must touch only one module. Cross-module side effects must be " +
                "published as integration events through IIntegrationEventPublisher so the " +
                "Wolverine outbox can deliver them transactionally.");
        }

        var db = dirtyContexts[0];
        var moduleParticipants = _participants.Where(p => ReferenceEquals(p.Owner, db)).ToList();

        // Reuse a transaction a participant opened early; otherwise open one now.
        var transaction = db.Database.CurrentTransaction
            ?? await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("Using transaction {TransactionId} for {RequestType}",
            transaction.TransactionId, typeof(TRequest).Name);
        try
        {
            // Flush the handler's staged writes — EF first, then the participants — inside the transaction.
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await FlushParticipantsAsync(moduleParticipants, cancellationToken).ConfigureAwait(false);

            // Drain-loop: dispatch domain events (including second-generation events raised by event
            // handlers) until none remain. Integration events are published here — by domain-event
            // handlers running inside this open transaction — so the publisher's "active transaction
            // required" guard is satisfied and the outbox envelope enrols this same transaction.
            bool hasMore = true;
            while (hasMore)
            {
                var events = db.ChangeTracker
                    .Entries<IHasDomainEvents>()
                    .SelectMany(e => e.Entity.TakeDomainEvents())
                    .Concat(moduleParticipants.SelectMany(p => p.TakeDomainEvents()))
                    .ToList();

                if (events.Count == 0)
                {
                    hasMore = false;
                    continue;
                }

                _logger.LogDebug("Dispatching {Count} domain events", events.Count);
                await _dispatcher.DispatchAsync(events, cancellationToken).ConfigureAwait(false);

                // A domain-event handler must not write to a DIFFERENT module — via its context or a
                // participant. Re-check here because the pre-loop guard only saw the handler's own writes.
                var foreignDirty = _dbContexts.Select(c => c.Instance).FirstOrDefault(other =>
                    !ReferenceEquals(other, db) &&
                    (other.ChangeTracker.HasChanges() ||
                     _participants.Any(p => p.HasPendingChanges && ReferenceEquals(p.Owner, other))));
                if (foreignDirty is not null)
                {
                    throw new InvalidOperationException(
                        $"A domain-event handler for {typeof(TRequest).Name} mutated a different module " +
                        $"DbContext ({foreignDirty.GetType().Name}). Cross-module side effects must " +
                        "be published as integration events through IIntegrationEventPublisher.");
                }

                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await FlushParticipantsAsync(moduleParticipants, cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("Committed transaction {TransactionId} for {RequestType}",
                transaction.TransactionId, typeof(TRequest).Name);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);

            // RollbackAsync reverts the database but leaves the entities tracked in their staged state on the
            // scoped context. Clear them (and the participants) so they can't be re-flushed by a later
            // ITransactional command sharing this scope.
            db.ChangeTracker.Clear();
            foreach (var participant in _participants)
            {
                participant.Discard();
            }

            _logger.LogWarning("Rolled back transaction {TransactionId} for {RequestType}",
                transaction.TransactionId, typeof(TRequest).Name);

            // EF's optimistic-concurrency failure and the event store's version conflict are the same thing to a
            // client: reload and retry. Surface both as the one domain exception (409 concurrency_conflict).
            if (ex is DbUpdateConcurrencyException)
            {
                throw new ConcurrencyConflictException(
                    $"{typeof(TRequest).Name} lost a concurrency race: the data was modified by another request. Reload and retry.",
                    ex);
            }

            throw;
        }
        finally
        {
            await transaction.DisposeAsync().ConfigureAwait(false);
        }

        // The command's data AND the integration-event envelopes it enrolled are now committed atomically.
        // Release the envelopes so they are sent immediately instead of on the messaging layer's next durable
        // recovery sweep. Reached only on a successful commit — the catch above rethrows.
        await FlushOutboxAsync(cancellationToken).ConfigureAwait(false);

        // Run post-commit side effects the handler deferred (e.g. authz cache-eviction nudges). These MUST
        // run after commit: firing them from inside the handler would evict/publish before the write is
        // durable, so a concurrent read could re-cache stale data.
        await RunPostCommitActionsAsync(cancellationToken).ConfigureAwait(false);

        return response;
    }

    private static async ValueTask FlushParticipantsAsync(
        IReadOnlyList<ITransactionParticipant> moduleParticipants,
        CancellationToken cancellationToken)
    {
        foreach (var participant in moduleParticipants)
        {
            if (participant.HasPendingChanges)
            {
                await participant.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Rolls back and disposes any module transaction that is still open when there is nothing to commit
    /// (a participant opened it early and the command then failed, or staged nothing). Best-effort per
    /// context; the exception that caused the failure — if any — still propagates from the caller.
    /// </summary>
    private async ValueTask ReleaseOpenTransactionsAsync(bool discardParticipants)
    {
        if (discardParticipants)
        {
            foreach (var participant in _participants)
            {
                participant.Discard();
            }
        }

        foreach (var context in _dbContexts)
        {
            IDbContextTransaction? open = context.Instance.Database.CurrentTransaction;
            if (open is null)
            {
                continue;
            }

            try
            {
                await open.RollbackAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Rolling back an early-opened transaction for {RequestType} failed.", typeof(TRequest).Name);
            }
            finally
            {
                await open.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Runs actions queued via <see cref="IPostCommitActions"/> after a successful commit. Best-effort: a
    /// failure is logged and swallowed because the command is already committed — failing it now would be
    /// wrong, and cache-eviction actions are self-healing via their TTL backstop.
    /// </summary>
    private async ValueTask RunPostCommitActionsAsync(CancellationToken cancellationToken)
    {
        foreach (var action in _postCommitActions.Drain())
        {
            try
            {
                await action(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Post-commit action failed for {RequestType}; the command is already committed.",
                    typeof(TRequest).Name);
            }
        }
    }

    /// <summary>
    /// Invokes every registered <see cref="IOutboxFlusher"/> after commit. Best-effort: a flush failure is
    /// swallowed (logged) because the envelope is already durably persisted — the recovery loop will still
    /// deliver it, and failing an already-committed command would be wrong. No-op when none is registered.
    /// </summary>
    private async ValueTask FlushOutboxAsync(CancellationToken cancellationToken)
    {
        foreach (var flusher in _outboxFlushers)
        {
            try
            {
                await flusher.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Outbox flush after commit failed for {RequestType}; the persisted envelope(s) will be " +
                    "delivered by the durable recovery loop instead.", typeof(TRequest).Name);
            }
        }
    }
}
