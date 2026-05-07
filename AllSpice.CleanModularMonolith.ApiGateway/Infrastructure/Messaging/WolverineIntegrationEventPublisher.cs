using AllSpice.CleanModularMonolith.SharedKernel.Messaging;
using AllSpice.CleanModularMonolith.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Wolverine;
using Wolverine.EntityFrameworkCore;

namespace AllSpice.CleanModularMonolith.ApiGateway.Infrastructure.Messaging;

/// <summary>
/// Publishes integration events through Wolverine's durable outbox so that the message
/// is committed atomically with the originating command's database transaction.
/// Refuses to publish outside an active transaction — callers must run inside an
/// <c>ITransactional</c> command so <c>TransactionBehavior</c> has opened a DbContext
/// transaction beforehand.
/// </summary>
public sealed class WolverineIntegrationEventPublisher : IIntegrationEventPublisher
{
    private readonly IDbContextOutbox _outbox;
    private readonly IEnumerable<IModuleDbContext> _dbContexts;

    public WolverineIntegrationEventPublisher(IDbContextOutbox outbox, IEnumerable<IModuleDbContext> dbContexts)
    {
        _outbox = outbox;
        _dbContexts = dbContexts;
    }

    public async ValueTask PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();

        var transactionalContext = _dbContexts
            .FirstOrDefault(c => c.Instance.Database.CurrentTransaction is not null);

        if (transactionalContext is null)
        {
            throw new InvalidOperationException(
                $"Cannot publish {typeof(T).Name}: no active DbContext transaction. " +
                "Integration events must be published from inside a command implementing ITransactional " +
                "so the message is enrolled in the durable outbox and rolled back with the transaction.");
        }

        // Enroll the active DbContext into the outbox. Wolverine's behavior on repeat
        // Enroll calls within the same scope is to swap the active context; we only
        // ever pass the same instance for a given scope so this is effectively a no-op
        // after the first call. After enrollment, PublishAsync writes to the wolverine
        // envelope tables inside the same transaction; rollback discards the message,
        // commit ships it.
        _outbox.Enroll(transactionalContext.Instance);

        await _outbox.PublishAsync(message);
    }
}
