using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by `dotnet ef migrations` to instantiate a DbContext
/// without booting the full host. Reads connection details from environment
/// variables so credentials are never committed to source.
/// </summary>
public sealed class NotificationsDbContextDesignTimeFactory : IDesignTimeDbContextFactory<NotificationsDbContext>
{
    private const string DatabaseName = "notificationsdb";

    public NotificationsDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("EF_DESIGN_NOTIFICATIONS_CONNECTION")
            ?? Environment.GetEnvironmentVariable("EF_DESIGN_CONNECTION")
            ?? BuildLocalDevConnectionString();

        var optionsBuilder = new DbContextOptionsBuilder<NotificationsDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        return new NotificationsDbContext(optionsBuilder.Options);
    }

    private static string BuildLocalDevConnectionString()
    {
        var host = Environment.GetEnvironmentVariable("EF_DESIGN_DB_HOST") ?? "localhost";
        var user = Environment.GetEnvironmentVariable("EF_DESIGN_DB_USER") ?? "postgres";
        var password = Environment.GetEnvironmentVariable("EF_DESIGN_DB_PASSWORD");
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Set EF_DESIGN_DB_PASSWORD (or a full connection string in EF_DESIGN_NOTIFICATIONS_CONNECTION / EF_DESIGN_CONNECTION) for design-time migrations.");
        }

        return $"Host={host};Database={DatabaseName};Username={user};Password={password}";
    }
}
