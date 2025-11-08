using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

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

    public static async Task<TestSqliteDatabase> CreateAsync()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseSqlite(connection)
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


