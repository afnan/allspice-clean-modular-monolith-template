using AllSpice.CleanModularMonolith.ApiGateway.Infrastructure.Messaging;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Messaging.Consumers;
using AllSpice.CleanModularMonolith.SharedKernel.Events;
using AllSpice.CleanModularMonolith.SharedKernel.Messaging;
using Wolverine;
using Wolverine.ErrorHandling;

namespace AllSpice.CleanModularMonolith.ApiGateway.Extensions;

/// <summary>
/// Provides helpers for registering application modules with the gateway host.
/// </summary>
public static class GatewayModuleRegistrationExtensions
{
    /// <summary>
    /// Registers all gateway modules. Extend this method as additional modules come online.
    /// </summary>
    /// <param name="builder">The web application builder being configured.</param>
    public static void RegisterGatewayModules(this WebApplicationBuilder builder)
    {
        using var loggerFactory = LoggerFactory.Create(logging => logging.AddConsole());
        var logger = loggerFactory.CreateLogger("Program");

        builder.Services.AddScoped<IDomainEventDispatcher, MediatorDomainEventDispatcher>();

        builder.AddNotificationsModuleServices(logger);
        builder.AddIdentityModuleServices(logger);

        builder.Host.UseWolverine(opts =>
        {
            opts.Discovery.IncludeAssembly(typeof(NotificationRequestedIntegrationEventConsumer).Assembly);

            opts.OnException<Exception>().RetryWithCooldown(
                TimeSpan.FromMilliseconds(500),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(30));
        });

        builder.Services.AddScoped<IIntegrationEventPublisher, WolverineIntegrationEventPublisher>();
    }
}
