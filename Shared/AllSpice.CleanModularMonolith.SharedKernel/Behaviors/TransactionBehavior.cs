using AllSpice.CleanModularMonolith.SharedKernel.Events;
using AllSpice.CleanModularMonolith.SharedKernel.Persistence;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AllSpice.CleanModularMonolith.SharedKernel.Behaviors;

/// <summary>
/// Pipeline behavior that owns the unit-of-work boundary for <see cref="ITransactional"/> commands.
/// <para>
/// Repositories stage writes only (see <c>EfRepository.SaveChangesAsync</c>), so the handler performs no
/// database writes itself. After the handler returns, this behavior finds the single dirty module
/// <see cref="IModuleDbContext"/>, opens a transaction <b>on that context</b>, flushes the staged writes,
/// drains domain events (which may stage more and may publish integration events that enrol the same
/// transaction's outbox), then commits — or rolls everything back on any failure.
/// </para>
/// <para>
/// This is what makes commands atomic for <b>every</b> module regardless of DI registration order. The
/// previous implementation opened the transaction on the first registered context before the handler ran,
/// which silently targeted the wrong database for any module that was not registered first.
/// </para>
/// </summary>
public sealed class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : class, IMessage, ITransactional
{
    private readonly IEnumerable<IModuleDbContext> _dbContexts;
    private readonly IDomainEventDispatcher _dispatcher;
    private readonly ILogger<TransactionBehavior<TRequest, TResponse>> _logger;

    public TransactionBehavior(
        IEnumerable<IModuleDbContext> dbContexts,
        IDomainEventDispatcher dispatcher,
        ILogger<TransactionBehavior<TRequest, TResponse>> logger)
    {
        _dbContexts = dbContexts;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    public async ValueTask<TResponse> Handle(
        TRequest request,
        MessageHandlerDelegate<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        // Repositories stage only (see EfRepository.SaveChangesAsync), so the handler performs no DB
        // writes itself. If it throws, nothing was flushed and there is nothing to roll back.
        var response = await next(request, cancellationToken).ConfigureAwait(false);

        // Exactly one module DbContext may be dirty. Cross-module side effects must go through
        // integration events (Wolverine outbox), never a direct write to another module's context.
        var dirtyContexts = _dbContexts
            .Where(c => c.Instance.ChangeTracker.HasChanges())
            .ToList();

        if (dirtyContexts.Count == 0)
        {
            return response; // nothing staged — no transaction needed
        }

        if (dirtyContexts.Count > 1)
        {
            var contextNames = string.Join(", ", dirtyContexts.Select(c => c.Instance.GetType().Name));
            throw new InvalidOperationException(
                $"{typeof(TRequest).Name} mutated multiple module DbContexts ({contextNames}). " +
                "A command must touch only one module. Cross-module side effects must be " +
                "published as integration events through IIntegrationEventPublisher so the " +
                "Wolverine outbox can deliver them transactionally.");
        }

        var db = dirtyContexts[0].Instance;
        var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("Began transaction {TransactionId} for {RequestType}",
            transaction.TransactionId, typeof(TRequest).Name);
        try
        {
            // Flush the handler's staged writes inside the transaction.
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

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
                    .ToList();

                if (events.Count == 0)
                {
                    hasMore = false;
                    continue;
                }

                _logger.LogDebug("Dispatching {Count} domain events", events.Count);
                await _dispatcher.DispatchAsync(events, cancellationToken).ConfigureAwait(false);

                // A domain-event handler must not write to a DIFFERENT module's context — the
                // pre-loop guard only saw the handler's own writes, so re-check here. A foreign
                // dirty context would otherwise be silently dropped (it's not flushed and has no
                // transaction). Cross-module side effects must go through integration events.
                var foreignDirty = _dbContexts.FirstOrDefault(c =>
                    !ReferenceEquals(c.Instance, db) && c.Instance.ChangeTracker.HasChanges());
                if (foreignDirty is not null)
                {
                    throw new InvalidOperationException(
                        $"A domain-event handler for {typeof(TRequest).Name} mutated a different module " +
                        $"DbContext ({foreignDirty.Instance.GetType().Name}). Cross-module side effects must " +
                        "be published as integration events through IIntegrationEventPublisher.");
                }

                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("Committed transaction {TransactionId} for {RequestType}",
                transaction.TransactionId, typeof(TRequest).Name);

            return response;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogWarning("Rolled back transaction {TransactionId} for {RequestType}",
                transaction.TransactionId, typeof(TRequest).Name);
            throw;
        }
        finally
        {
            await transaction.DisposeAsync().ConfigureAwait(false);
        }
    }
}
