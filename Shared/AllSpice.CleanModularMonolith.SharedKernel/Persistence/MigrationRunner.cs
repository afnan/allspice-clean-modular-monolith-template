using Microsoft.EntityFrameworkCore;
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
    /// Runs <see cref="DatabaseFacade.MigrateAsync"/> with linear backoff. The optional
    /// <paramref name="seedAsync"/> delegate executes after a successful migrate inside the
    /// same retry envelope so that seed work also benefits from the retry policy.
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
