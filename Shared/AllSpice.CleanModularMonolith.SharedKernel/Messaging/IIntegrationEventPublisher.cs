namespace AllSpice.CleanModularMonolith.SharedKernel.Messaging;

public interface IIntegrationEventPublisher
{
    ValueTask PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class;
}
