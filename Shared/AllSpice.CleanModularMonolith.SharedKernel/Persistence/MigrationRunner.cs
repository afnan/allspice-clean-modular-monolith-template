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
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await context.Database.MigrateAsync(cancellationToken);

                if (seedAsync is not null)
                {
                    await seedAsync(context, cancellationToken);
                }

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
}
