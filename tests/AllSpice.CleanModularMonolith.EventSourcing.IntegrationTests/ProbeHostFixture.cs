using AllSpice.CleanModularMonolith.EventSourcing.IntegrationTests.Probe;
using AllSpice.CleanModularMonolith.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace AllSpice.CleanModularMonolith.EventSourcing.IntegrationTests;

/// <summary>
/// One Postgres container + one host per test class. The host wires the probe DbContext and the probe
/// Marten store exactly the way a module would (AddModuleEventStore) — EF tables via EnsureCreated, Marten
/// schema via ApplyEventStoreSchemaAsync.
/// </summary>
public sealed class ProbeHostFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public IHost Host { get; private set; } = null!;

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
        builder.Services.AddDbContext<ProbeDbContext>(o => o.UseNpgsql(ConnectionString));
        builder.Services.AddScoped<IModuleDbContext>(sp => sp.GetRequiredService<ProbeDbContext>());
        // Registering the live-stream aggregation is what makes Marten treat the Events feature as "active"
        // for an ancillary store: without at least one event type/projection registration, an ancillary
        // store's schema migration (ApplyEventStoreSchemaAsync) silently skips mt_streams/mt_events entirely
        // (only the empty schema namespace gets created), even though FetchForWriting<T> resolves ProbeCounter
        // fine at runtime via live aggregation. This is the generic `configure` hook AddModuleEventStore
        // already exposes — no SharedKernel/EventSourcing production change needed.
        builder.AddModuleEventStore<IProbeEventStore, ProbeDbContext>(ConnectionString, "probe",
            opts => opts.Projections.LiveStreamAggregation<ProbeCounter>());
        builder.Services.AddScoped<ProbeCounterRepository>();

        Host = builder.Build();

        await using (var scope = Host.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ProbeDbContext>().Database.EnsureCreatedAsync();
        }

        await Host.Services.ApplyEventStoreSchemaAsync<IProbeEventStore>();
        await Host.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await Host.StopAsync();
        Host.Dispose();
        await _postgres.DisposeAsync();
    }
}
