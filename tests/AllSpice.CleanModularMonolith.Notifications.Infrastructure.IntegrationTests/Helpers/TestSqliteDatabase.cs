using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.IntegrationTests.Helpers;

internal sealed class TestSqliteDatabase : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly NotificationsDbContext _context;

    private TestSqliteDatabase(SqliteConnection connection, NotificationsDbContext context)
    {
        _connection = connection;
        _context = context;
    }

    public NotificationsDbContext Context => _context;

    /// <summary>
    /// Creates an additional <see cref="NotificationsDbContext"/> bound to the SAME in-memory database
    /// (shared connection) but with its own change tracker — used to simulate two dispatcher replicas
    /// racing to claim the same row. The caller owns disposal.
    /// </summary>
    public NotificationsDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseSqlite(_connection)
            .ReplaceService<IModelCustomizer, SqliteJsonbModelCustomizer>()
            .EnableSensitiveDataLogging()
            .Options;

        return new NotificationsDbContext(options);
    }

    public static async Task<TestSqliteDatabase> CreateAsync()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseSqlite(connection)
            .ReplaceService<IModelCustomizer, SqliteJsonbModelCustomizer>()
            .EnableSensitiveDataLogging()
            .Options;

        var context = new NotificationsDbContext(options);
        await context.Database.EnsureCreatedAsync();

        return new TestSqliteDatabase(connection, context);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }
}


