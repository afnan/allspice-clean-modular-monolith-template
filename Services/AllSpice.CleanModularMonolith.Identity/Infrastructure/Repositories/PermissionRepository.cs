using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence;
using AllSpice.CleanModularMonolith.SharedKernel.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Repositories;

public sealed class PermissionRepository(IdentityDbContext dbContext)
    : EfRepository<IdentityDbContext, Permission>(dbContext), IPermissionRepository
{
    private readonly IdentityDbContext _dbContext = dbContext;

    public Task<Permission?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
        => _dbContext.Permissions.FirstOrDefaultAsync(p => p.Key == key, cancellationToken);

    public async Task<IReadOnlyList<Permission>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        return await _dbContext.Permissions
            .AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .ToListAsync(cancellationToken);
    }
}
