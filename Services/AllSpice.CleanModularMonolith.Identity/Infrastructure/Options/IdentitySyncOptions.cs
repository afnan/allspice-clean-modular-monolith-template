namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Options;

/// <summary>
/// Options governing the Authentik synchronization job.
/// </summary>
public sealed class IdentitySyncOptions
{
    public const string ConfigurationSectionName = "Identity:Sync";

    /// <summary>
    /// Cron expression that determines how frequently the sync job runs.
    /// </summary>
    public string CronExpression { get; set; } = "0 0/15 * * * ?";

    /// <summary>
    /// The maximum number of users fetched per Authentik request.
    /// </summary>
    public int PageSize { get; set; } = 100;

    /// <summary>
    /// Maximum acceptable age for the last successful sync before health checks report unhealthy.
    /// </summary>
    public TimeSpan MaxSuccessAge { get; set; } = TimeSpan.FromMinutes(30);
}


