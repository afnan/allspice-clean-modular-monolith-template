using AllSpice.CleanModularMonolith.Identity.Infrastructure.Jobs;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Options;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Services;

/// <summary>
/// Health check that ensures the Keycloak synchronization job has succeeded recently.
/// </summary>
public sealed class IdentitySyncHealthCheck : IHealthCheck
{
    private readonly IdentityDbContext _dbContext;
    private readonly IOptions<IdentitySyncOptions> _options;
    private readonly ILogger<IdentitySyncHealthCheck> _logger;

    public IdentitySyncHealthCheck(
        IdentityDbContext dbContext,
        IOptions<IdentitySyncOptions> options,
        ILogger<IdentitySyncHealthCheck> logger)
    {
        _dbContext = dbContext;
        _options = options;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var lastSuccess = await _dbContext.IdentitySyncHistories
            .Where(history => history.JobName == KeycloakUserSyncJob.JobIdentity && history.Succeeded)
            .OrderByDescending(history => history.FinishedUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (lastSuccess is null)
        {
            const string description = "No successful Keycloak syncs have been recorded.";
            _logger.LogWarning(description);
            return HealthCheckResult.Unhealthy(description);
        }

        var age = DateTimeOffset.UtcNow - lastSuccess.FinishedUtc;
        if (age > _options.Value.MaxSuccessAge)
        {
            var message = $"Last Keycloak sync succeeded {age:g} ago which exceeds the allowed threshold.";
            _logger.LogWarning(message);
            return HealthCheckResult.Degraded(
                message,
                data: new Dictionary<string, object>
                {
                    ["lastSuccessUtc"] = lastSuccess.FinishedUtc.UtcDateTime,
                    ["ageMinutes"] = age.TotalMinutes
                });
        }

        return HealthCheckResult.Healthy();
    }
}


