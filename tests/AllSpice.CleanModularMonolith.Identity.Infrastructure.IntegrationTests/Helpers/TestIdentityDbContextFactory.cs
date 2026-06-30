using AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.IntegrationTests.Helpers;

/// <summary>
/// Creates a SQLite-backed <see cref="IdentityDbContext"/> for integration tests.
/// The returned context is ready to use; call <c>await using</c> on it to dispose.
/// The underlying in-memory SQLite connection is held alive by EF Core's internal
/// connection management for the lifetime of the returned context.
/// </summary>
internal static class TestIdentityDbContextFactory
{
    public static async Task<IdentityDbContext> CreateAsync()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlite(connection)
            .ReplaceService<IModelCustomizer, SqliteJsonbModelCustomizer>()
            .EnableSensitiveDataLogging()
            .Options;

        var context = new IdentityDbContext(options);
        await context.Database.EnsureCreatedAsync();

        return context;
    }
}
