using AllSpice.CleanModularMonolith.EventSourcing;
using AllSpice.CleanModularMonolith.Ledger.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Ledger.Infrastructure.Persistence;
using AllSpice.CleanModularMonolith.Ledger.Infrastructure.Repositories;
using AllSpice.CleanModularMonolith.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Testcontainers.PostgreSql;

namespace AllSpice.CleanModularMonolith.Ledger.Infrastructure.IntegrationTests;

/// <summary>
/// Wires the Ledger persistence exactly as production does (same store configuration, same repository) minus
/// Wolverine/Aspire, against a throwaway Postgres.
/// </summary>
public sealed class LedgerHostFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public IHost Host { get; private set; } = null!;

    /// <summary>Exposed so a test can open a SEPARATE store over the same database (see the legacy-upcast test) — the
    /// host's own store must stay configured with the unmodified, production <see cref="LedgerEventStoreConfiguration.Configure"/>.</summary>
    public string ConnectionString { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        var cs = _postgres.GetConnectionString();
        ConnectionString = cs;

        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
        builder.Services.AddDbContext<LedgerDbContext>(o => o.UseNpgsql(cs));
        builder.Services.AddScoped<IModuleDbContext>(sp => sp.GetRequiredService<LedgerDbContext>());
        builder.AddModuleEventStore<ILedgerEventStore, LedgerDbContext>(cs, LedgerEventStoreConfiguration.SchemaName, LedgerEventStoreConfiguration.Configure);
        builder.Services.AddScoped<IAccountRepository, AccountRepository>();
        Host = builder.Build();

        await using (var scope = Host.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<LedgerDbContext>().Database.MigrateAsync();
        }

        await Host.Services.ApplyEventStoreSchemaAsync<ILedgerEventStore>();

        // Controller addendum: assert the store's schema apply actually created the event tables (rather
        // than silently skipping them — see ApplyEventStoreSchemaAsync's guard for what happens otherwise).
        await using (var connection = new NpgsqlConnection(cs))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT to_regclass('ledger.mt_events')::text";
            var result = await command.ExecuteScalarAsync();
            if (result is null or DBNull)
            {
                throw new InvalidOperationException("ledger.mt_events was not created by ApplyEventStoreSchemaAsync.");
            }
        }

        await Host.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await Host.StopAsync();
        Host.Dispose();
        await _postgres.DisposeAsync();
    }
}
