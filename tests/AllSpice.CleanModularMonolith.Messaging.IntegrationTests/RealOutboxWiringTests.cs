using AllSpice.CleanModularMonolith.Identity.Infrastructure.Entities;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.Persistence.Durability;
using Wolverine.Postgresql;

namespace AllSpice.CleanModularMonolith.Messaging.IntegrationTests;

/// <summary>
/// Exercises the REAL template wiring: the actual IdentityDbContext and NotificationsDbContext, each with a
/// Wolverine durable outbox co-located in its OWN database (identitydb = main store, notificationsdb =
/// enrolled ancillary), and NO separate messaging database. Proves an integration event published inside an
/// Identity command transaction is committed atomically with the business row and then delivered.
/// </summary>
public sealed class RealOutboxWiringTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _identityDb = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
    private readonly PostgreSqlContainer _notificationsDb = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
    private IHost _host = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_identityDb.StartAsync(), _notificationsDb.StartAsync());
        var identityConn = _identityDb.GetConnectionString();
        var notificationsConn = _notificationsDb.GetConnectionString();

        var builder = Host.CreateApplicationBuilder();

        // Same registration the module extensions use.
        builder.Services.AddDbContextWithWolverineIntegration<IdentityDbContext>(o => o.UseNpgsql(identityConn));
        builder.Services.AddDbContextWithWolverineIntegration<NotificationsDbContext>(o => o.UseNpgsql(notificationsConn));

        // Same Wolverine config the gateway uses: per-module co-located stores, no shared messagingdb.
        builder.UseWolverine(opts =>
        {
            opts.PersistMessagesWithPostgresql(identityConn, "wolverine");
            opts.PersistMessagesWithPostgresql(notificationsConn, "wolverine", MessageStoreRole.Ancillary)
                .Enroll<NotificationsDbContext>();
            opts.UseEntityFrameworkCoreTransactions();
            opts.Policies.UseDurableLocalQueues();
            opts.Discovery.IncludeAssembly(typeof(RealOutboxWiringTests).Assembly);
        });

        _host = builder.Build();

        await using (var scope = _host.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.EnsureCreatedAsync();
            await scope.ServiceProvider.GetRequiredService<NotificationsDbContext>().Database.EnsureCreatedAsync();
        }

        await _host.StartAsync();

        // Same startup provisioning Program.cs performs.
        foreach (var store in _host.Services.GetServices<IMessageStore>())
        {
            await store.Admin.MigrateAsync();
        }
    }

    public async Task DisposeAsync()
    {
        await _host.StopAsync();
        _host.Dispose();
        await Task.WhenAll(_identityDb.DisposeAsync().AsTask(), _notificationsDb.DisposeAsync().AsTask());
    }

    [Fact]
    public async Task Identity_command_publishes_through_its_own_co_located_outbox_atomically()
    {
        var probeId = Guid.NewGuid();
        var correlationId = probeId.ToString("N");
        var signal = ProbeEventHandler.Register(probeId);

        // Mirrors WolverineIntegrationEventPublisher: enroll the active module DbContext, publish.
        await using (var scope = _host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var outbox = scope.ServiceProvider.GetRequiredService<IDbContextOutbox>();

            await using var tx = await db.Database.BeginTransactionAsync();
            db.IdentitySyncHistories.Add(new IdentitySyncHistory
            {
                JobName = "outbox-probe",
                StartedUtc = DateTimeOffset.UtcNow,
                CorrelationId = correlationId
            });

            outbox.Enroll(db);
            await outbox.PublishAsync(new ProbeEvent(probeId));

            await db.SaveChangesAsync();
            await tx.CommitAsync();
            await outbox.FlushOutgoingMessagesAsync();
        }

        var delivered = await Task.WhenAny(signal.Task, Task.Delay(TimeSpan.FromSeconds(30)));
        Assert.True(delivered == signal.Task, "Integration event from Identity's co-located outbox was not delivered.");

        await using var verifyScope = _host.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        Assert.True(
            await verifyDb.IdentitySyncHistories.AnyAsync(h => h.CorrelationId == correlationId),
            "Business row was not committed in identitydb.");
    }
}
