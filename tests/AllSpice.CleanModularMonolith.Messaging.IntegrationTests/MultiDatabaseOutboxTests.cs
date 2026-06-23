using JasperFx.Resources;
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
/// Mirrors the real template topology: a module business database that hosts its own outgoing envelope
/// tables (registered as an Ancillary Wolverine store), and a SEPARATE main control/message database
/// (messagingdb). Proves an event published from the module-DB outbox commits atomically with the
/// business row and is then relayed/delivered.
/// </summary>
public sealed class MultiDatabaseOutboxTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _businessDb = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
    private readonly PostgreSqlContainer _messagingDb = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
    private IHost _host = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_businessDb.StartAsync(), _messagingDb.StartAsync());
        var businessConn = _businessDb.GetConnectionString();
        var messagingConn = _messagingDb.GetConnectionString();

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddDbContextWithWolverineIntegration<OutboxProbeDbContext>(o => o.UseNpgsql(businessConn));
        builder.Services.AddResourceSetupOnStartup();

        builder.UseWolverine(opts =>
        {
            // Separate main control/message store (the template's messagingdb) ...
            opts.PersistMessagesWithPostgresql(messagingConn, "wolverine", MessageStoreRole.Main);
            // ... and the module's own database as an Ancillary store, ENROLLED to its DbContext so the
            // co-located outbox is provisioned and relayed.
            opts.PersistMessagesWithPostgresql(businessConn, "wolverine", MessageStoreRole.Ancillary)
                .Enroll<OutboxProbeDbContext>();

            opts.UseEntityFrameworkCoreTransactions();
            opts.Policies.UseDurableLocalQueues();
            opts.Discovery.IncludeAssembly(typeof(MultiDatabaseOutboxTests).Assembly);
        });

        _host = builder.Build();

        await using (var scope = _host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OutboxProbeDbContext>();
            await db.Database.EnsureCreatedAsync();
        }

        await _host.StartAsync();

        // Force provisioning of ALL Wolverine stores, including the enrolled module database, by
        // migrating each registered message store directly.
        await _host.SetupResources();
        foreach (var store in _host.Services.GetServices<IMessageStore>())
        {
            await store.Admin.MigrateAsync();
        }
    }

    public async Task DisposeAsync()
    {
        await _host.StopAsync();
        _host.Dispose();
        await Task.WhenAll(_businessDb.DisposeAsync().AsTask(), _messagingDb.DisposeAsync().AsTask());
    }

    [Fact]
    public async Task Event_from_module_db_outbox_is_delivered_with_a_separate_main_store()
    {
        var probeId = Guid.NewGuid();
        var signal = ProbeEventHandler.Register(probeId);

        await using (var scope = _host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OutboxProbeDbContext>();
            var outbox = scope.ServiceProvider.GetRequiredService<IDbContextOutbox>();

            await using var tx = await db.Database.BeginTransactionAsync();
            db.Probes.Add(new ProbeRow { Id = probeId, Name = "multi-db" });

            outbox.Enroll(db);
            await outbox.PublishAsync(new ProbeEvent(probeId));

            await db.SaveChangesAsync();
            await tx.CommitAsync();
            await outbox.FlushOutgoingMessagesAsync();
        }

        var delivered = await Task.WhenAny(signal.Task, Task.Delay(TimeSpan.FromSeconds(30)));
        Assert.True(delivered == signal.Task, "Event from the module-DB outbox was not delivered with a separate main store.");

        await using var verifyScope = _host.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<OutboxProbeDbContext>();
        Assert.True(await verifyDb.Probes.AnyAsync(p => p.Id == probeId), "Business row was not committed.");
    }
}
