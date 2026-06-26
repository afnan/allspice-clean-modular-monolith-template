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

namespace AllSpice.CleanModularMonolith.Foundation.IntegrationTests;

/// <summary>
/// Exercises the REAL hybrid outbox topology: messagingdb is the Wolverine MAIN store (shared infra only),
/// and the real IdentityDbContext + NotificationsDbContext are ANCILLARY stores hosting their OWN co-located
/// outbox envelopes. Proves co-location two ways:
/// <list type="bullet">
/// <item>commit through Identity's enrolled outbox → event delivered AND business row persisted in identitydb;</item>
/// <item>rollback of Identity's transaction → NOTHING delivered. This is the decisive co-location proof: the
/// envelope must live inside identitydb's own transaction, otherwise a rollback there could not suppress it.</item>
/// </list>
/// </summary>
public sealed class HybridOutboxTopologyTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _messagingDb = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private readonly PostgreSqlContainer _identityDb = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private readonly PostgreSqlContainer _notificationsDb = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private IHost _host = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_messagingDb.StartAsync(), _identityDb.StartAsync(), _notificationsDb.StartAsync());
        var messagingConn = _messagingDb.GetConnectionString();
        var identityConn = _identityDb.GetConnectionString();
        var notificationsConn = _notificationsDb.GetConnectionString();

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddDbContextWithWolverineIntegration<IdentityDbContext>(o => o.UseNpgsql(identityConn));
        builder.Services.AddDbContextWithWolverineIntegration<NotificationsDbContext>(o => o.UseNpgsql(notificationsConn));

        // Same hybrid topology as the gateway: messagingdb main (infra only), module DBs ancillary (outbox).
        builder.UseWolverine(opts =>
        {
            opts.PersistMessagesWithPostgresql(messagingConn, "wolverine");
            opts.PersistMessagesWithPostgresql(identityConn, "wolverine", MessageStoreRole.Ancillary)
                .Enroll<IdentityDbContext>();
            opts.PersistMessagesWithPostgresql(notificationsConn, "wolverine", MessageStoreRole.Ancillary)
                .Enroll<NotificationsDbContext>();
            opts.UseEntityFrameworkCoreTransactions();
            opts.Policies.UseDurableLocalQueues();
            opts.Discovery.IncludeAssembly(typeof(HybridOutboxTopologyTests).Assembly);
        });

        _host = builder.Build();

        // Provision exactly the way production (Program.cs) does: EF migrations create the BUSINESS
        // tables (which do NOT contain Wolverine envelope tables), then Wolverine's Admin.MigrateAsync
        // provisions the co-located envelope tables. Using MigrateAsync (not EnsureCreatedAsync) means a
        // broken ancillary-store provisioning genuinely fails this test instead of being masked.
        await using (var scope = _host.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
            await scope.ServiceProvider.GetRequiredService<NotificationsDbContext>().Database.MigrateAsync();
        }

        await _host.StartAsync();

        foreach (var store in _host.Services.GetServices<IMessageStore>())
        {
            await store.Admin.MigrateAsync();
        }
    }

    public async Task DisposeAsync()
    {
        await _host.StopAsync();
        _host.Dispose();
        await Task.WhenAll(
            _messagingDb.DisposeAsync().AsTask(),
            _identityDb.DisposeAsync().AsTask(),
            _notificationsDb.DisposeAsync().AsTask());
    }

    [Fact]
    public async Task Identity_event_commits_atomically_through_its_co_located_ancillary_outbox()
    {
        var probeId = Guid.NewGuid();
        var correlationId = probeId.ToString("N");
        var signal = ProbeEventHandler.Register(probeId);

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
        Assert.True(delivered == signal.Task, "Identity event from its co-located ancillary outbox was not delivered.");

        await using var verifyScope = _host.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        Assert.True(
            await verifyDb.IdentitySyncHistories.AnyAsync(h => h.CorrelationId == correlationId),
            "Business row was not committed in identitydb.");
    }

    [Fact]
    public async Task Rolled_back_identity_transaction_delivers_no_event()
    {
        var probeId = Guid.NewGuid();
        var correlationId = probeId.ToString("N");
        var signal = ProbeEventHandler.Register(probeId);

        await using (var scope = _host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var outbox = scope.ServiceProvider.GetRequiredService<IDbContextOutbox>();

            await using var tx = await db.Database.BeginTransactionAsync();
            db.IdentitySyncHistories.Add(new IdentitySyncHistory
            {
                JobName = "outbox-probe-rollback",
                StartedUtc = DateTimeOffset.UtcNow,
                CorrelationId = correlationId
            });

            outbox.Enroll(db);
            await outbox.PublishAsync(new ProbeEvent(probeId));

            await db.SaveChangesAsync();
            await tx.RollbackAsync(); // co-located envelope must roll back with the business write
        }

        var delivered = await Task.WhenAny(signal.Task, Task.Delay(TimeSpan.FromSeconds(8)));
        Assert.False(delivered == signal.Task, "Event was delivered despite a rolled-back Identity transaction — envelope is not co-located.");

        await using var verifyScope = _host.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        Assert.False(
            await verifyDb.IdentitySyncHistories.AnyAsync(h => h.CorrelationId == correlationId),
            "Business row persisted despite rollback.");
    }
}
