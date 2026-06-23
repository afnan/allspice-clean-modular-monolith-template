using AllSpice.CleanModularMonolith.SharedKernel.Messaging;
using AllSpice.CleanModularMonolith.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Wolverine;
using Wolverine.EntityFrameworkCore;

namespace AllSpice.CleanModularMonolith.ApiGateway.Infrastructure.Messaging;

/// <summary>
/// Publishes integration events through Wolverine's durable outbox for reliable at-least-once
/// delivery with crash recovery.
/// <para>
/// Refuses to publish outside an active transaction — callers must run inside an
/// <c>ITransactional</c> command so <c>TransactionBehavior</c> has opened a DbContext transaction
/// beforehand. Because the behavior now opens that transaction on the <b>single dirty module
/// context</b>, the lookup below resolves exactly that context and enrols it into the outbox.
/// </para>
/// <para>
/// Outbox atomicity depends on where the envelope tables live: once they are co-located in each
/// module's own database (see the per-module Wolverine wiring), the envelope insert and the command's
/// data commit are one atomic transaction. Until then the envelope store is separate and the two
/// commits are independent.
/// </para>
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
                "so publication is scoped to a command context and enrolled in the durable outbox.");
        }

        // Enroll the active DbContext into the outbox. Wolverine's behavior on repeat
        // Enroll calls within the same scope is to swap the active context; we only
        // ever pass the same instance for a given scope so this is effectively a no-op
        // after the first call. PublishAsync then persists the envelope to the durable
        // outbox store for at-least-once delivery. (See the type-level note: with the
        // shared messagingdb store this persistence is NOT in the command's transaction.)
        _outbox.Enroll(transactionalContext.Instance);

        await _outbox.PublishAsync(message);
    }
}
