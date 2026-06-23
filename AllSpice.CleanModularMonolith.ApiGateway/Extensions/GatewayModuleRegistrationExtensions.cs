using AllSpice.CleanModularMonolith.ApiGateway.Infrastructure.Messaging;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Messaging.Consumers;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Persistence;
using AllSpice.CleanModularMonolith.SharedKernel.Events;
using AllSpice.CleanModularMonolith.SharedKernel.Messaging;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.ErrorHandling;
using Wolverine.Persistence.Durability;
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

        var identityConnectionString = builder.Configuration.GetConnectionString("identitydb");
        var notificationsConnectionString = builder.Configuration.GetConnectionString("notificationsdb");
        if (string.IsNullOrWhiteSpace(identityConnectionString) || string.IsNullOrWhiteSpace(notificationsConnectionString))
        {
            throw new InvalidOperationException(
                "Connection strings 'identitydb' and 'notificationsdb' are required. Each module hosts its own " +
                "Wolverine durable outbox co-located with its data; ensure the AppHost references both database " +
                "resources on the gateway project.");
        }

        builder.Host.UseWolverine(opts =>
        {
            opts.Discovery.IncludeAssembly(typeof(NotificationRequestedIntegrationEventConsumer).Assembly);

            // EF Core transaction middleware: Wolverine wraps handlers in EF Core transactions
            opts.UseEntityFrameworkCoreTransactions();

            // True transactional outbox: each module's durable outbox/inbox is co-located in its OWN
            // database (see {Module}DbContext.OnModelCreating + AddDbContextWithWolverineIntegration), so an
            // integration event is persisted in the SAME transaction as the command's business data. There is
            // no shared messaging database. Identity's database is the main Wolverine store; Notifications is
            // an enrolled ancillary store. The non-main store's schema is provisioned at startup (Program.cs).
            opts.PersistMessagesWithPostgresql(identityConnectionString, "wolverine");
            opts.PersistMessagesWithPostgresql(notificationsConnectionString, "wolverine", MessageStoreRole.Ancillary)
                .Enroll<NotificationsDbContext>();

            // Make every transport durable by default.
            opts.Policies.UseDurableLocalQueues();
            opts.Policies.UseDurableInboxOnAllListeners();
            opts.Policies.UseDurableOutboxOnAllSendingEndpoints();

            // Retry only TYPED transient failures; programming errors and unknown
            // exceptions go straight to the error queue / dead-letter via Wolverine's
            // default policy. Previous code retried InvalidOperationException broadly,
            // which trapped genuine bugs in an infinite refire loop.
            opts.OnException<TransientMessagingException>().RetryWithCooldown(
                TimeSpan.FromMilliseconds(500),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(30))
                .Then.MoveToErrorQueue();

            opts.OnException<TimeoutException>().RetryWithCooldown(
                TimeSpan.FromMilliseconds(500),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(30))
                .Then.MoveToErrorQueue();

            opts.OnException<HttpRequestException>().RetryWithCooldown(
                TimeSpan.FromMilliseconds(500),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(30))
                .Then.MoveToErrorQueue();
        });

        builder.Services.AddScoped<IIntegrationEventPublisher, WolverineIntegrationEventPublisher>();
    }
}
