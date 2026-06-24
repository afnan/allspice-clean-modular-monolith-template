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
/// The envelope tables are co-located in each module's own database (see
/// <c>{Module}DbContext.MapWolverineEnvelopeStorage</c> + the gateway's ancillary-store registration),
/// so the envelope insert and the command's data commit are ONE atomic transaction — a crash can no
/// longer drop or orphan an event. The shared <c>messagingdb</c> store holds only Wolverine's
/// cross-module infrastructure (inbox, durable local queues, scheduled, dead-letter), never business
/// outbox envelopes.
/// </para>
/// </summary>
public sealed class WolverineIntegrationEventPublisher(IDbContextOutbox outbox, IEnumerable<IModuleDbContext> dbContexts) : IIntegrationEventPublisher
{
    private readonly IDbContextOutbox _outbox = outbox;
    private readonly IEnumerable<IModuleDbContext> _dbContexts = dbContexts;

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

        // Enroll the active DbContext (the single one TransactionBehavior opened a transaction on)
        // into the outbox, then persist the envelope. Because the envelope tables are co-located in
        // that module's own database, the envelope is written in the SAME transaction as the command's
        // business data — committed atomically when TransactionBehavior commits.
        _outbox.Enroll(transactionalContext.Instance);

        await _outbox.PublishAsync(message);
    }
}
