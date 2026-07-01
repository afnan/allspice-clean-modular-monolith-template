using AllSpice.CleanModularMonolith.ApiGateway.Identity;
using AllSpice.CleanModularMonolith.ApiGateway.Infrastructure.Messaging;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Messaging.Consumers;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Persistence;
using AllSpice.CleanModularMonolith.SharedKernel.Behaviors;
using AllSpice.CleanModularMonolith.SharedKernel.Events;
using AllSpice.CleanModularMonolith.SharedKernel.Identity;
using AllSpice.CleanModularMonolith.SharedKernel.Interceptors;
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
        // Scoped queue drained by TransactionBehavior after commit — lets handlers defer best-effort side
        // effects (e.g. authz cache eviction) until the write is durable instead of firing them pre-commit.
        builder.Services.AddScoped<IPostCommitActions, PostCommitActions>();

        // Current-user provider for audit stamping + cross-cutting EF Core save interceptors
        // (concurrency diagnostics, audit-user stamping). AddSharedKernelInterceptors registers them
        // explicitly as IInterceptor singletons; EF Core then resolves those services from this
        // container and applies them to every module DbContext (it does not scan for interceptors).
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<ICurrentUserProvider, HttpContextCurrentUserProvider>();
        // Per-request cache for the resolved canonical local user id; populated once per request by
        // CurrentUserResolutionMiddleware so audit stamping records the local UUID, not the IdP subject.
        builder.Services.AddScoped<ICurrentUserContext, CurrentUserContext>();
        builder.Services.AddSharedKernelInterceptors();

        builder.AddNotificationsModuleServices(logger);
        builder.AddIdentityModuleServices(logger);

        // messagingdb is the Wolverine MAIN store: it holds ONLY shared messaging infrastructure
        // (inbox, durable local queues, scheduled messages, dead-letter, node/agent coordination).
        // Each module's database is an ANCILLARY store holding only its OWN outbox envelopes, so an
        // integration event commits atomically with the business data that produced it.
        var messagingConnectionString = builder.Configuration.GetConnectionString("messagingdb");
        var identityConnectionString = builder.Configuration.GetConnectionString("identitydb");
        var notificationsConnectionString = builder.Configuration.GetConnectionString("notificationsdb");
        if (string.IsNullOrWhiteSpace(messagingConnectionString)
            || string.IsNullOrWhiteSpace(identityConnectionString)
            || string.IsNullOrWhiteSpace(notificationsConnectionString))
        {
            throw new InvalidOperationException(
                "Connection strings 'messagingdb', 'identitydb' and 'notificationsdb' are required. messagingdb " +
                "holds shared Wolverine infrastructure (inbox/queues/scheduled/dead-letter); each module database " +
                "hosts its own co-located transactional outbox. Ensure the AppHost references all three on the gateway.");
        }

        builder.Host.UseWolverine(opts =>
        {
            opts.Discovery.IncludeAssembly(typeof(NotificationRequestedIntegrationEventConsumer).Assembly);

            // EF Core transaction middleware: Wolverine wraps handlers in EF Core transactions
            opts.UseEntityFrameworkCoreTransactions();

            // MAIN store (shared infrastructure only). Auto-builds its schema on startup.
            opts.PersistMessagesWithPostgresql(messagingConnectionString, "wolverine");

            // ANCILLARY stores: each module DB hosts its own outbox envelopes, enrolled so the
            // WolverineIntegrationEventPublisher writes the envelope in the module's own transaction.
            // The ancillary schemas are provisioned explicitly at startup (see Program.cs).
            opts.PersistMessagesWithPostgresql(identityConnectionString, "wolverine", MessageStoreRole.Ancillary)
                .Enroll<IdentityDbContext>();
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

        // Lets TransactionBehavior release the durable outbox right after commit (prompt send instead of
        // waiting for Wolverine's recovery sweep). Scoped so it shares the publisher's IDbContextOutbox.
        builder.Services.AddScoped<IOutboxFlusher, WolverineOutboxFlusher>();
    }
}
