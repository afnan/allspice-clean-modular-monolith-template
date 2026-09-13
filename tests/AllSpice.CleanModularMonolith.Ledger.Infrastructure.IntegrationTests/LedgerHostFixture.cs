using AllSpice.CleanModularMonolith.EventSourcing;
using AllSpice.CleanModularMonolith.Ledger.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Ledger.Domain.Events.Legacy;
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

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        var cs = _postgres.GetConnectionString();

        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
        builder.Services.AddDbContext<LedgerDbContext>(o => o.UseNpgsql(cs));
        builder.Services.AddScoped<IModuleDbContext>(sp => sp.GetRequiredService<LedgerDbContext>());
        // Test-only addition: FundsDepositedV1 is registered ONLY as an Upcast source in production config
        // (LedgerEventStoreConfiguration never calls AddEventType/MapEventType for it — it is never raised by
        // real code), so Marten has no writable mapping for it and Append() stores it under its own default
        // type name ("funds_deposited_v1"), never triggering the upcast. The legacy-upcast test needs to
        // write a row under the OLD stored name ("funds_deposited") the way a pre-versioning deployment would
        // have, so — and ONLY here, never in production config — explicitly map FundsDepositedV1 to that name
        // so a direct Append() lands under it and is upcast to FundsDeposited on read, exactly like history.
        builder.AddModuleEventStore<ILedgerEventStore, LedgerDbContext>(cs, LedgerEventStoreConfiguration.SchemaName, opts =>
        {
            LedgerEventStoreConfiguration.Configure(opts);
            opts.Events.MapEventType<FundsDepositedV1>("funds_deposited");
        });
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
