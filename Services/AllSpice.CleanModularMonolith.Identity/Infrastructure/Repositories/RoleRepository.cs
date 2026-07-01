using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence;
using AllSpice.CleanModularMonolith.SharedKernel.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Repositories;

public sealed class RoleRepository(IdentityDbContext dbContext)
    : EfRepository<IdentityDbContext, Role>(dbContext), IRoleRepository
{
    private readonly IdentityDbContext _dbContext = dbContext;

    // .ToLower() translates to SQL LOWER() — case-insensitive and provider-agnostic (works on SQLite tests + Postgres).
    // Role.Key is already stored lowercase.
    public Task<Role?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
        => _dbContext.Roles.FirstOrDefaultAsync(r => r.Key.ToLower() == key.ToLower(), cancellationToken);
}
