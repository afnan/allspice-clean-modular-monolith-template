using AllSpice.CleanModularMonolith.EventSourcing;
using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using AllSpice.CleanModularMonolith.Ledger.Infrastructure.Authorization;
using AllSpice.CleanModularMonolith.SharedKernel.HealthChecks;
using AllSpice.CleanModularMonolith.SharedKernel.Persistence;
using Wolverine.EntityFrameworkCore;

namespace AllSpice.CleanModularMonolith.Ledger.Infrastructure.Extensions;

/// <summary>
/// Wires the Ledger module — the template's reference EVENT-SOURCED module (ADR-0009) — into the host.
/// Everything is the standard module recipe plus one call: <c>AddModuleEventStore</c>.
/// </summary>
public static class LedgerModuleExtensions
{
    private const string DatabaseResourceName = "ledgerdb";

    public static IHostApplicationBuilder AddLedgerModuleServices(this IHostApplicationBuilder builder, ILogger logger)
    {
        builder.Services.AddSingleton<IModulePermissionManifest, LedgerPermissionManifest>();

        var connectionString = builder.Configuration[$"ConnectionStrings:{DatabaseResourceName}"]
            ?? throw new InvalidOperationException($"Connection string '{DatabaseResourceName}' is required for the Ledger module.");

        // Same registration as the other modules: the context owns the transaction and hosts the co-located
        // outbox; shared-kernel interceptors are attached explicitly (EF Core does not discover them from DI).
        builder.Services.AddDbContextWithWolverineIntegration<LedgerDbContext>((sp, options) => options
            .UseNpgsql(connectionString)
            .AddInterceptors(sp.GetServices<Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor>()));
        builder.EnrichNpgsqlDbContext<LedgerDbContext>(settings =>
        {
            settings.DisableRetry = true;          // user-initiated transactions forbid the retrying strategy
            settings.DisableHealthChecks = true;   // DbContextHealthCheck<LedgerDbContext> below covers it
        });
        builder.Services.AddScoped<IModuleDbContext>(sp => sp.GetRequiredService<LedgerDbContext>());

        // The opt-in: a Marten store in ledgerdb (schema "ledger") enlisted in LedgerDbContext's transaction.
        builder.AddModuleEventStore<ILedgerEventStore, LedgerDbContext>(
            connectionString,
            LedgerEventStoreConfiguration.SchemaName,
            LedgerEventStoreConfiguration.Configure);

        builder.Services.AddScoped<IAccountRepository, AccountRepository>();

        builder.Services.AddMediator();
        builder.Services.AddValidatorsFromAssembly(typeof(AppAssemblyReference).Assembly);

        builder.Services.AddHealthChecks()
            .AddCheck<DbContextHealthCheck<LedgerDbContext>>("ledger-db");

        logger.LogInformation("Ledger module services registered (event-sourced Account via Marten)");
        return builder;
    }

    /// <summary>EF migration (outbox tables) under the advisory lock, then the Marten schema for the event store.</summary>
    public static async Task<WebApplication> EnsureLedgerModuleDatabaseAsync(this WebApplication app)
    {
        await MigrationRunner.RunForModuleAsync<LedgerDbContext>(app.Services, app.Lifetime, loggerCategory: "LedgerDatabase");
        await app.Services.ApplyEventStoreSchemaAsync<ILedgerEventStore>(app.Lifetime.ApplicationStopping);
        return app;
    }
}
