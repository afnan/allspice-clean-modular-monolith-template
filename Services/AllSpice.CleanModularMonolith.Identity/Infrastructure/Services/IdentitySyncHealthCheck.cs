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
public sealed class IdentitySyncHealthCheck(
    IdentityDbContext dbContext,
    IOptions<IdentitySyncOptions> options,
    IOptions<KeycloakOptions> keycloakOptions,
    TimeProvider timeProvider,
    ILogger<IdentitySyncHealthCheck> logger) : IHealthCheck
{
    private readonly IdentityDbContext _dbContext = dbContext;
    private readonly IOptions<IdentitySyncOptions> _options = options;
    private readonly IOptions<KeycloakOptions> _keycloakOptions = keycloakOptions;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ILogger<IdentitySyncHealthCheck> _logger = logger;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!_keycloakOptions.Value.IsAdminConfigured)
        {
            // No IdP linked yet — the sync job has nothing to talk to. Report Degraded (not Unhealthy) so the
            // app stays "up and running"; this becomes a real freshness check once Keycloak is configured.
            return HealthCheckResult.Degraded("Keycloak is not linked yet — user sync is idle until Identity:Keycloak is configured.");
        }

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

        var age = _timeProvider.GetUtcNow() - lastSuccess.FinishedUtc;
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


