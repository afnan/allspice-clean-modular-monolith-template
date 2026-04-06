using AllSpice.CleanModularMonolith.SharedKernel.Messaging;
using Wolverine;

namespace AllSpice.CleanModularMonolith.ApiGateway.Infrastructure.Messaging;

public sealed class WolverineIntegrationEventPublisher(IMessageBus bus) : IIntegrationEventPublisher
{
    public ValueTask PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        return bus.PublishAsync(message);
    }
}
