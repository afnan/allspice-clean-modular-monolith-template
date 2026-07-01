using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Repositories;

public sealed class AuthzMapVersionRepository(IdentityDbContext dbContext) : IAuthzMapVersionRepository
{
    private readonly IdentityDbContext _dbContext = dbContext;

    public async Task<AuthzMapVersion> GetTrackedAsync(CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.AuthzMapVersions.FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var created = AuthzMapVersion.Initial();
        _dbContext.AuthzMapVersions.Add(created);
        return created;
    }
}
