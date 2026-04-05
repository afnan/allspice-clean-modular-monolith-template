using AllSpice.CleanModularMonolith.SharedKernel.Events;
using AllSpice.CleanModularMonolith.SharedKernel.Persistence;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace AllSpice.CleanModularMonolith.SharedKernel.Behaviors;

/// <summary>
/// Pipeline behavior that wraps transactional commands in an explicit database transaction.
/// Domain events are dispatched after the handler completes but before the transaction commits,
/// ensuring atomicity between state changes and event processing.
/// Only activates for requests implementing <see cref="ITransactional"/>.
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
        IDbContextTransaction? transaction = null;

        foreach (var ctx in _dbContexts)
        {
            var db = ctx.Instance;
            if (db.Database.CurrentTransaction is null)
            {
                transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogDebug("Began transaction {TransactionId} for {RequestType}",
                    transaction.TransactionId, typeof(TRequest).Name);
                break;
            }
        }

        try
        {
            var response = await next(request, cancellationToken).ConfigureAwait(false);

            // Drain-loop: dispatch domain events, including second-generation events
            // raised by event handlers, until no more remain.
            foreach (var ctx in _dbContexts)
            {
                bool hasMore = true;
                while (hasMore)
                {
                    var events = ctx.Instance.ChangeTracker
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
                    await ctx.Instance.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogDebug("Committed transaction {TransactionId} for {RequestType}",
                    transaction.TransactionId, typeof(TRequest).Name);
            }

            return response;
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogWarning("Rolled back transaction {TransactionId} for {RequestType}",
                    transaction.TransactionId, typeof(TRequest).Name);
            }

            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
