using Npgsql;
using Testcontainers.PostgreSql;

namespace AllSpice.CleanModularMonolith.Foundation.IntegrationTests;

/// <summary>
/// Shared Testcontainers PostgreSQL instance for foundation integration tests.
/// Each test creates its own freshly-named databases on the one container so the
/// transaction/outbox guarantees can be proven against real Postgres (SQLite cannot
/// reproduce multi-context transactions or the Wolverine durable outbox).
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public string AdminConnectionString => _container.GetConnectionString();

    public async Task<string> CreateDatabaseAsync(string dbName)
    {
        await using var conn = new NpgsqlConnection(AdminConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"CREATE DATABASE \"{dbName}\"";
        await cmd.ExecuteNonQueryAsync();

        var builder = new NpgsqlConnectionStringBuilder(AdminConnectionString) { Database = dbName };
        return builder.ConnectionString;
    }

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(nameof(PostgresCollection))]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;
