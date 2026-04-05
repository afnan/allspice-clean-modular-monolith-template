using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence;

public sealed class IdentityDbContextDesignTimeFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<IdentityDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=identitydb;Username=postgres;Password=pass!");
        return new IdentityDbContext(optionsBuilder.Options);
    }
}
