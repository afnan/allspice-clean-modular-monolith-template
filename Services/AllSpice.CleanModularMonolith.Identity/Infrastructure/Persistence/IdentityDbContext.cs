using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.ModuleDefinition;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.ModuleRoleAssignment;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.ModuleRoleTemplate;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence;

public sealed class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options)
        : base(options)
    {
    }

    public DbSet<ModuleDefinition> ModuleDefinitions => Set<ModuleDefinition>();

    public DbSet<ModuleRoleAssignment> ModuleRoleAssignments => Set<ModuleRoleAssignment>();

    public DbSet<ModuleRoleTemplate> ModuleRoleTemplates => Set<ModuleRoleTemplate>();

    public DbSet<IdentitySyncHistory> IdentitySyncHistories => Set<IdentitySyncHistory>();

    public DbSet<IdentityOrphanUser> IdentityOrphanUsers => Set<IdentityOrphanUser>();

    public DbSet<IdentitySyncIssue> IdentitySyncIssues => Set<IdentitySyncIssue>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
    }
}


