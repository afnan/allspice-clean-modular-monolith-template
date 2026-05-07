using AllSpice.CleanModularMonolith.ApiGateway.Infrastructure.Messaging;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Messaging.Consumers;
using AllSpice.CleanModularMonolith.SharedKernel.Events;
using AllSpice.CleanModularMonolith.SharedKernel.Messaging;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.ErrorHandling;
using Wolverine.Postgresql;

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

        var messagingConnectionString = builder.Configuration.GetConnectionString("messagingdb");
        if (string.IsNullOrWhiteSpace(messagingConnectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'messagingdb' is required. Wolverine is configured for durable PostgreSQL persistence " +
                "and refuses to start in-memory. Ensure the AppHost references the messagingdb resource on the gateway project.");
        }

        builder.Host.UseWolverine(opts =>
        {
            opts.Discovery.IncludeAssembly(typeof(NotificationRequestedIntegrationEventConsumer).Assembly);

            // EF Core transaction middleware: Wolverine wraps handlers in EF Core transactions
            opts.UseEntityFrameworkCoreTransactions();

            // Durable outbox/inbox: store message envelopes in PostgreSQL ("wolverine" schema).
            // AutoBuildMessageStorageOnStartup defaults to CreateOrUpdate, so the schema is
            // provisioned automatically on first run — no manual migration required.
            opts.PersistMessagesWithPostgresql(messagingConnectionString, "wolverine");

            // Make every transport durable by default.
            opts.Policies.UseDurableLocalQueues();
            opts.Policies.UseDurableInboxOnAllListeners();
            opts.Policies.UseDurableOutboxOnAllSendingEndpoints();

            // Retry only transient failures; non-transient errors go straight to error queue
            opts.OnException<TimeoutException>().RetryWithCooldown(
                TimeSpan.FromMilliseconds(500),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(30));
            opts.OnException<HttpRequestException>().RetryWithCooldown(
                TimeSpan.FromMilliseconds(500),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(30));
            opts.OnException<InvalidOperationException>().RetryWithCooldown(
                TimeSpan.FromMilliseconds(500),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(30));
        });

        builder.Services.AddScoped<IIntegrationEventPublisher, WolverineIntegrationEventPublisher>();
    }
}
