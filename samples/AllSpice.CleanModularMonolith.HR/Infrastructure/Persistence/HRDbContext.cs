using Microsoft.EntityFrameworkCore;
using AllSpice.CleanModularMonolith.HR.Domain.Aggregates;

namespace AllSpice.CleanModularMonolith.HR.Infrastructure.Persistence;

public class HRDbContext : DbContext
{
    public HRDbContext(DbContextOptions<HRDbContext> options)
        : base(options)
    {
    }

    public DbSet<Employee> Employees => Set<Employee>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HRDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}


