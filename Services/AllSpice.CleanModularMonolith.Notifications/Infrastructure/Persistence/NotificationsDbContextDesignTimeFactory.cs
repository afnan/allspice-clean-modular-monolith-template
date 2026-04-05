using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Persistence;

public sealed class NotificationsDbContextDesignTimeFactory : IDesignTimeDbContextFactory<NotificationsDbContext>
{
    public NotificationsDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<NotificationsDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=notificationsdb;Username=postgres;Password=pass!");
        return new NotificationsDbContext(optionsBuilder.Options);
    }
}
