using AllSpice.CleanModularMonolith.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AllSpice.CleanModularMonolith.EventSourcing.IntegrationTests.Probe;

public sealed class ProbeRow
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>Stands in for a module DbContext: owns the transaction and has one ordinary EF table.</summary>
public sealed class ProbeDbContext(DbContextOptions<ProbeDbContext> options) : DbContext(options), IModuleDbContext
{
    public DbSet<ProbeRow> Rows => Set<ProbeRow>();

    public DbContext Instance => this;

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<ProbeRow>().HasKey(r => r.Id);
}

public interface IProbeEventStore : Marten.IDocumentStore
{
}
