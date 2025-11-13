using AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Services;

/// <summary>
/// Health check that reports when unresolved orphan users exist.
/// </summary>
public sealed class IdentityOrphanHealthCheck : IHealthCheck
{
    private readonly IdentityDbContext _dbContext;

    public IdentityOrphanHealthCheck(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var orphanCount = await _dbContext.IdentityOrphanUsers
            .CountAsync(orphan => orphan.ResolvedUtc == null, cancellationToken);

        if (orphanCount == 0)
        {
            return HealthCheckResult.Healthy();
        }

        return HealthCheckResult.Degraded(
            $"There are {orphanCount} Authentik users without module role assignments.",
            data: new Dictionary<string, object>
            {
                ["orphanCount"] = orphanCount
            });
    }
}


