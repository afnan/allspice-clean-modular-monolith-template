using AllSpice.CleanModularMonolith.SharedKernel.Messaging;
using Wolverine.EntityFrameworkCore;

namespace AllSpice.CleanModularMonolith.ApiGateway.Infrastructure.Messaging;

/// <summary>
/// Wolverine-backed <see cref="IOutboxFlusher"/>. Flushes the scoped <see cref="IDbContextOutbox"/> that
/// <see cref="WolverineIntegrationEventPublisher"/> enrolled during the command's transaction — both are
/// resolved in the same DI (request) scope, so this sees exactly the messages that command buffered. Called
/// by <c>TransactionBehavior</c> after commit, it sends those persisted envelopes immediately instead of
/// waiting for Wolverine's durable recovery sweep.
/// </summary>
public sealed class WolverineOutboxFlusher(IDbContextOutbox outbox) : IOutboxFlusher
{
    private readonly IDbContextOutbox _outbox = outbox;

    public ValueTask FlushAsync(CancellationToken cancellationToken = default) =>
        new(_outbox.FlushOutgoingMessagesAsync());
}
