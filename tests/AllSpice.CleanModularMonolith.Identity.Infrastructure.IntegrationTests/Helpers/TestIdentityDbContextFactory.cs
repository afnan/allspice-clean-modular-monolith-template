using AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

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

    /// <summary>
    /// Creates a <see cref="SharedConnectionHarness"/> that holds a single open <see cref="SqliteConnection"/>
    /// and a seeded <see cref="IdentityDbContext"/> over it. The harness exposes
    /// <see cref="SharedConnectionHarness.RegisterDbContext"/> so that a DI <see cref="ServiceCollection"/>
    /// can register <see cref="IdentityDbContext"/> over the SAME connection — ensuring the DI-resolved
    /// <c>PermissionMapStore</c> reads the rows seeded via <see cref="SharedConnectionHarness.Context"/>.
    /// </summary>
    public static Task<SharedConnectionHarness> CreateSharedAsync()
        => SharedConnectionHarness.CreateAsync();
}

/// <summary>
/// Holds an open <see cref="SqliteConnection"/> and an <see cref="IdentityDbContext"/> built on it.
/// Call <see cref="RegisterDbContext"/> to wire the same connection into a DI <see cref="ServiceCollection"/>
/// so that all scoped <see cref="IdentityDbContext"/> instances share the same in-memory SQLite database.
/// Dispose to close both the context and the underlying connection.
/// </summary>
internal sealed class SharedConnectionHarness : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    public IdentityDbContext Context { get; }

    private SharedConnectionHarness(SqliteConnection connection, IdentityDbContext context)
    {
        _connection = connection;
        Context = context;
    }

    internal static async Task<SharedConnectionHarness> CreateAsync()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var context = new IdentityDbContext(BuildOptions(connection));
        await context.Database.EnsureCreatedAsync();

        return new SharedConnectionHarness(connection, context);
    }

    /// <summary>
    /// Registers <see cref="IdentityDbContext"/> over the SAME open <see cref="SqliteConnection"/> so
    /// that any scope created by the DI container reads the rows seeded via <see cref="Context"/>.
    /// </summary>
    public void RegisterDbContext(IServiceCollection services)
    {
        services.AddDbContext<IdentityDbContext>(opt =>
            opt.UseSqlite(_connection)
               .ReplaceService<IModelCustomizer, SqliteJsonbModelCustomizer>()
               .EnableSensitiveDataLogging());
    }

    private static DbContextOptions<IdentityDbContext> BuildOptions(SqliteConnection connection)
        => new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlite(connection)
            .ReplaceService<IModelCustomizer, SqliteJsonbModelCustomizer>()
            .EnableSensitiveDataLogging()
            .Options;

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
