using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.Postgresql;

namespace AllSpice.CleanModularMonolith.Foundation.IntegrationTests;

/// <summary>
/// Proves the transactional-outbox guarantee with envelope storage co-located in the business database:
/// a published integration event commits atomically with the business write and is then delivered; a
/// rolled-back transaction delivers nothing and leaves no business row. Uses a self-contained probe
/// DbContext so the guarantee is tested in isolation from the module schemas. Mirrors the publish shape of
/// <c>WolverineIntegrationEventPublisher</c> (enroll the active context, publish).
/// </summary>
public sealed class OutboxAtomicityTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    private IHost _host = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        var connectionString = _postgres.GetConnectionString();

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddDbContextWithWolverineIntegration<OutboxProbeDbContext>(o => o.UseNpgsql(connectionString));
        builder.UseWolverine(opts =>
        {
            opts.PersistMessagesWithPostgresql(connectionString, "wolverine");
            opts.UseEntityFrameworkCoreTransactions();
            opts.Policies.UseDurableLocalQueues();
            opts.Discovery.IncludeAssembly(typeof(OutboxAtomicityTests).Assembly);
        });

        _host = builder.Build();

        // Create business + envelope tables while the DB is empty, BEFORE Wolverine provisions its store.
        await using (var scope = _host.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<OutboxProbeDbContext>().Database.EnsureCreatedAsync();
        }

        await _host.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _host.StopAsync();
        _host.Dispose();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task Committed_command_publishes_the_event_and_persists_business_row()
    {
        var probeId = Guid.NewGuid();
        var signal = ProbeEventHandler.Register(probeId);

        await using (var scope = _host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OutboxProbeDbContext>();
            var outbox = scope.ServiceProvider.GetRequiredService<IDbContextOutbox>();

            await using var tx = await db.Database.BeginTransactionAsync();
            db.Probes.Add(new ProbeRow { Id = probeId, Name = "committed" });

            outbox.Enroll(db);
            await outbox.PublishAsync(new ProbeEvent(probeId));

            await db.SaveChangesAsync();
            await tx.CommitAsync();
            await outbox.FlushOutgoingMessagesAsync();
        }

        var delivered = await Task.WhenAny(signal.Task, Task.Delay(TimeSpan.FromSeconds(30)));
        Assert.True(delivered == signal.Task, "Integration event was not delivered after commit.");

        await using var verifyScope = _host.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<OutboxProbeDbContext>();
        Assert.True(await verifyDb.Probes.AnyAsync(p => p.Id == probeId), "Business row was not committed.");
    }

    [Fact]
    public async Task Rolled_back_command_publishes_nothing_and_persists_no_row()
    {
        var probeId = Guid.NewGuid();
        var signal = ProbeEventHandler.Register(probeId);

        await using (var scope = _host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OutboxProbeDbContext>();
            var outbox = scope.ServiceProvider.GetRequiredService<IDbContextOutbox>();

            await using var tx = await db.Database.BeginTransactionAsync();
            db.Probes.Add(new ProbeRow { Id = probeId, Name = "rolled-back" });

            outbox.Enroll(db);
            await outbox.PublishAsync(new ProbeEvent(probeId));

            await db.SaveChangesAsync();
            await tx.RollbackAsync();
        }

        var delivered = await Task.WhenAny(signal.Task, Task.Delay(TimeSpan.FromSeconds(8)));
        Assert.False(delivered == signal.Task, "Integration event was delivered despite a rolled-back transaction.");

        await using var verifyScope = _host.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<OutboxProbeDbContext>();
        Assert.False(await verifyDb.Probes.AnyAsync(p => p.Id == probeId), "Business row persisted despite rollback.");
    }
}

public sealed record ProbeEvent(Guid ProbeId);

public static class ProbeEventHandler
{
    private static readonly ConcurrentDictionary<Guid, TaskCompletionSource> Signals = new();

    public static TaskCompletionSource Register(Guid probeId)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Signals[probeId] = tcs;
        return tcs;
    }

    // Wolverine message handler (discovered by convention).
    public static void Handle(ProbeEvent message)
    {
        if (Signals.TryGetValue(message.ProbeId, out var tcs))
        {
            tcs.TrySetResult();
        }
    }
}

public sealed class ProbeRow
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class OutboxProbeDbContext : DbContext
{
    public OutboxProbeDbContext(DbContextOptions<OutboxProbeDbContext> options) : base(options)
    {
    }

    public DbSet<ProbeRow> Probes => Set<ProbeRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProbeRow>().HasKey(p => p.Id);
        modelBuilder.MapWolverineEnvelopeStorage("wolverine");
    }
}
