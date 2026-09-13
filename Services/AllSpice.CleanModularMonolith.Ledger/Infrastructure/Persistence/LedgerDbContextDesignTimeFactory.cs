using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AllSpice.CleanModularMonolith.Ledger.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by `dotnet ef migrations` to instantiate a DbContext
/// without booting the full host. Reads connection details from environment
/// variables so credentials are never committed to source.
/// </summary>
public sealed class LedgerDbContextDesignTimeFactory : IDesignTimeDbContextFactory<LedgerDbContext>
{
    private const string DatabaseName = "ledgerdb";

    public LedgerDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("EF_DESIGN_LEDGER_CONNECTION")
            ?? Environment.GetEnvironmentVariable("EF_DESIGN_CONNECTION")
            ?? BuildLocalDevConnectionString();

        var optionsBuilder = new DbContextOptionsBuilder<LedgerDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        return new LedgerDbContext(optionsBuilder.Options);
    }

    private static string BuildLocalDevConnectionString()
    {
        var host = Environment.GetEnvironmentVariable("EF_DESIGN_DB_HOST") ?? "localhost";
        var user = Environment.GetEnvironmentVariable("EF_DESIGN_DB_USER") ?? "postgres";
        var password = Environment.GetEnvironmentVariable("EF_DESIGN_DB_PASSWORD");
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Set EF_DESIGN_DB_PASSWORD (or a full connection string in EF_DESIGN_LEDGER_CONNECTION / EF_DESIGN_CONNECTION) for design-time migrations.");
        }

        return $"Host={host};Database={DatabaseName};Username={user};Password={password}";
    }
}
