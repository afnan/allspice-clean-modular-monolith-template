namespace AllSpice.CleanModularMonolith.SharedKernel.Messaging;

/// <summary>
/// Cross-module integration event publisher. Modules raise integration events to
/// communicate with each other without sharing aggregates or DbContexts.
/// </summary>
/// <remarks>
/// The gateway-side implementation (<c>WolverineIntegrationEventPublisher</c>) writes
/// the message into the Wolverine durable outbox enrolled in the active DbContext
/// transaction, so events are committed atomically with the originating command and
/// rolled back on failure. Calling <see cref="PublishAsync"/> outside an
/// <c>ITransactional</c> command throws — there is no fire-and-forget path.
/// </remarks>
public interface IIntegrationEventPublisher
{
    /// <summary>
    /// Publishes an integration event through the durable outbox. Must be called
    /// from inside an <c>ITransactional</c> command so the message is enrolled in the
    /// active DB transaction.
    /// </summary>
    ValueTask PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class;
}
