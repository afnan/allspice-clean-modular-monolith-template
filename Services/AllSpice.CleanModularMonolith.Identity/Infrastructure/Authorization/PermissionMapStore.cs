using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Authorization;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Authorization;

public sealed class PermissionMapStore(IdentityDbContext dbContext) : IPermissionMapStore
{
    private readonly IdentityDbContext _dbContext = dbContext;

    public async Task<PermissionMap> GetMapAsync(CancellationToken cancellationToken)
    {
        // Read the version FIRST so a concurrent mutation can only make the cached data newer than its
        // version label, never the reverse — prevents caching stale-as-fresh.
        var version = await _dbContext.AuthzMapVersions
            .AsNoTracking()
            .Select(v => v.Version)
            .FirstOrDefaultAsync(cancellationToken);

        // roleKey -> set of permission keys, built from a single projection join.
        var rows = await (
            from rp in _dbContext.RolePermissions.AsNoTracking()
            join r in _dbContext.Roles.AsNoTracking() on rp.RoleId equals r.Id
            join p in _dbContext.Permissions.AsNoTracking() on rp.PermissionId equals p.Id
            select new { r.Key, PermissionKey = p.Key })
            .ToListAsync(cancellationToken);

        var map = rows
            .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlySet<string>)g.Select(x => x.PermissionKey).ToHashSet(StringComparer.Ordinal),
                StringComparer.OrdinalIgnoreCase);

        return new PermissionMap(version, map);
    }
}
