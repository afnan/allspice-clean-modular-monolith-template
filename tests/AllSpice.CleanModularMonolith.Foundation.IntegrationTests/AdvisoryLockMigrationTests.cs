using AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence;
using AllSpice.CleanModularMonolith.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AllSpice.CleanModularMonolith.Foundation.IntegrationTests;

/// <summary>
/// Proves the startup migration advisory lock: two instances migrating the same fresh database
/// concurrently both succeed (the lock serializes them), instead of racing the same un-applied
/// migration ("relation already exists" / partial apply).
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class AdvisoryLockMigrationTests(PostgresFixture pg)
{
    private static IdentityDbContext NewContext(string connectionString) =>
        new(new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(connectionString).Options);

    [Fact]
    public async Task Concurrent_migrations_against_same_db_both_succeed()
    {
        var connectionString = await pg.CreateDatabaseAsync("advlock_" + Guid.NewGuid().ToString("N"));

        await using var ctx1 = NewContext(connectionString);
        await using var ctx2 = NewContext(connectionString);

        var migrate1 = MigrationRunner.RunWithRetryAsync(ctx1, NullLogger.Instance, CancellationToken.None);
        var migrate2 = MigrationRunner.RunWithRetryAsync(ctx2, NullLogger.Instance, CancellationToken.None);

        // Must not throw despite both hitting the same fresh database concurrently.
        await Task.WhenAll(migrate1, migrate2);

        // Schema applied exactly once and is queryable.
        await using var verify = NewContext(connectionString);
        Assert.False(await verify.Users.AnyAsync());
    }
}
