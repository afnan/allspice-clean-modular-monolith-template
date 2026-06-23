using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using AllSpice.CleanModularMonolith.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Wolverine.EntityFrameworkCore;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Persistence;

public sealed class NotificationsDbContext : DbContext, IModuleDbContext
{
    public NotificationsDbContext(DbContextOptions<NotificationsDbContext> options)
        : base(options)
    {
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.ApplyUtcDateTimeOffsetConversions();
    }

    DbContext IModuleDbContext.Instance => this;

    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationsDbContext).Assembly);
        modelBuilder.ApplySoftDeleteFilters();

        // Co-locate the Wolverine durable outbox tables in this module's own database so integration
        // events commit in the SAME transaction as the business data (a true transactional outbox).
        // Guarded to Npgsql: the envelope storage uses Postgres-specific types, and the SQLite-based
        // integration tests must not try to create these tables.
        if (Database.IsNpgsql())
        {
            modelBuilder.MapWolverineEnvelopeStorage("wolverine");
        }
    }
}


