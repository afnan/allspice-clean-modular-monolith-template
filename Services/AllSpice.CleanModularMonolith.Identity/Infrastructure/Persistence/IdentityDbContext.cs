using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Invitation;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.User;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Entities;
using AllSpice.CleanModularMonolith.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Wolverine.EntityFrameworkCore;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence;

public sealed class IdentityDbContext : DbContext, IModuleDbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options)
        : base(options)
    {
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.ApplyUtcDateTimeOffsetConversions();
    }

    DbContext IModuleDbContext.Instance => this;

    public DbSet<User> Users => Set<User>();

    public DbSet<Invitation> Invitations => Set<Invitation>();

    public DbSet<IdentitySyncHistory> IdentitySyncHistories => Set<IdentitySyncHistory>();

    public DbSet<IdentityOrphanUser> IdentityOrphanUsers => Set<IdentityOrphanUser>();

    public DbSet<IdentitySyncIssue> IdentitySyncIssues => Set<IdentitySyncIssue>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
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


