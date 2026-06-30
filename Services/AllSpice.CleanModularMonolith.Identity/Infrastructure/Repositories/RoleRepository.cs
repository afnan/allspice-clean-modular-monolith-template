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

    // EF.Functions.ILike for case-insensitive match (Npgsql); the unique index is on Key.
    public Task<Role?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
        => _dbContext.Roles.FirstOrDefaultAsync(r => r.Key.ToLower() == key.ToLower(), cancellationToken);
}
