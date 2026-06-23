using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AllSpice.CleanModularMonolith.SharedKernel.Persistence;

/// <summary>
/// Helper for running EF Core migrations with retry on transient infrastructure failures.
/// Used at startup when modules wait for the database to become reachable.
/// </summary>
public static class MigrationRunner
{
    public const int DefaultMaxAttempts = 5;

    /// <summary>
    /// Convenience wrapper for module bootstrap: creates an async DI scope, resolves
    /// the module's DbContext, and runs <see cref="RunWithRetryAsync"/> against it.
    /// Both modules use the same scope+migrate+seed pattern, so it lives here once.
    /// </summary>
    /// <typeparam name="TContext">The module's DbContext type.</typeparam>
    /// <param name="services">Root service provider (typically <c>app.Services</c>).</param>
    /// <param name="lifetime">Host lifetime so cancellation flows through application shutdown.</param>
    /// <param name="loggerCategory">Logger category for migration diagnostics (e.g. "IdentityDatabase").</param>
    /// <param name="seedAsync">Optional seed delegate executed inside the same retry envelope as the migration.</param>
    /// <param name="maxAttempts">Maximum retry attempts. Defaults to <see cref="DefaultMaxAttempts"/>.</param>
    public static async Task RunForModuleAsync<TContext>(
        IServiceProvider services,
        IHostApplicationLifetime lifetime,
        string loggerCategory,
        Func<DbContext, CancellationToken, Task>? seedAsync = null,
        int maxAttempts = DefaultMaxAttempts)
        where TContext : DbContext
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(loggerCategory);

        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();

        await RunWithRetryAsync(
            context,
            logger,
            lifetime.ApplicationStopping,
            seedAsync,
            maxAttempts);
    }

    /// <summary>
    /// Runs <see cref="DatabaseFacade.MigrateAsync"/> with linear backoff. The optional
    /// <paramref name="seedAsync"/> delegate executes after a successful migrate inside the
    /// same retry envelope so that seed work also benefits from the retry policy.
    /// Throws after exhausting <paramref name="maxAttempts"/> so the host fails fast.
    /// </summary>
    public static async Task RunWithRetryAsync(
        DbContext context,
        ILogger logger,
        CancellationToken cancellationToken,
        Func<DbContext, CancellationToken, Task>? seedAsync = null,
        int maxAttempts = DefaultMaxAttempts)
    {
        // A non-positive count would skip the loop entirely and return without migrating —
        // exactly the "boot against an un-migrated DB" failure this method exists to prevent.
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await MigrateWithOptionalAdvisoryLockAsync(context, seedAsync, cancellationToken);
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Database migration attempt {Attempt}/{Max} failed for {DbContext}.",
                    attempt,
                    maxAttempts,
                    context.GetType().Name);

                if (attempt >= maxAttempts)
                {
                    // Exhausted retries — propagate so the host fails fast instead of
                    // silently booting against an un-migrated database.
                    throw;
                }

                await Task.Delay(TimeSpan.FromSeconds(attempt * 2), cancellationToken);
            }
        }
    }

    private const string NpgsqlProviderName = "Npgsql.EntityFrameworkCore.PostgreSQL";

    /// <summary>
    /// Runs the migration (and optional seed) under a cross-instance Postgres advisory lock so two
    /// gateway instances booting against the same database cannot race the same un-applied migration
    /// ("relation already exists" / partial apply). The lock is session-scoped, held on an explicitly
    /// opened connection for the whole migrate, and keyed per DbContext type. Non-Npgsql providers
    /// (e.g. the SQLite test databases) migrate directly — they need no cross-instance coordination and
    /// SharedKernel stays provider-agnostic (provider detected by name, no Npgsql package dependency).
    /// </summary>
    private static async Task MigrateWithOptionalAdvisoryLockAsync(
        DbContext context,
        Func<DbContext, CancellationToken, Task>? seedAsync,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(context.Database.ProviderName, NpgsqlProviderName, StringComparison.Ordinal))
        {
            await context.Database.MigrateAsync(cancellationToken);
            if (seedAsync is not null)
            {
                await seedAsync(context, cancellationToken);
            }

            return;
        }

        var lockKey = StableLockKey(context.GetType().FullName!);

        // Open the connection explicitly so the session-level advisory lock is held across MigrateAsync.
        await context.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_lock({0})", [lockKey], cancellationToken);
            try
            {
                await context.Database.MigrateAsync(cancellationToken);
                if (seedAsync is not null)
                {
                    await seedAsync(context, cancellationToken);
                }
            }
            finally
            {
                await context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_unlock({0})", [lockKey], cancellationToken);
            }
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    /// <summary>FNV-1a 64-bit hash — deterministic and stable across processes, unlike <see cref="string.GetHashCode()"/>.</summary>
    private static long StableLockKey(string name)
    {
        const ulong offsetBasis = 14695981039346656037;
        const ulong prime = 1099511628211;

        var hash = offsetBasis;
        foreach (var c in name)
        {
            hash ^= c;
            hash *= prime;
        }

        return unchecked((long)hash);
    }
}
