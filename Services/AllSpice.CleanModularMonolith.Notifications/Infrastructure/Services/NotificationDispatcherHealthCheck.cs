using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Services;

/// <summary>
/// Reports the health of the notification dispatcher background service.
/// Unhealthy when the service hasn't completed an iteration recently — catches
/// a silently-dead loop that wouldn't otherwise show up in logs or metrics.
/// </summary>
public sealed class NotificationDispatcherHealthCheck(
    NotificationDispatcherHealthState state,
    IOptions<NotificationDispatcherOptions> options,
    TimeProvider timeProvider) : IHealthCheck
{
    private const int StaleMultiplier = 3;

    // A single dispatch cycle sends its whole batch (up to 20 notifications — see DueNotificationsSpecification)
    // SYNCHRONOUSLY before it records another "last run". A slow provider can make that batch take far longer
    // than the poll interval, so the stale threshold must budget for the worst-case batch send time on top of
    // the poll cadence; otherwise a normal (if slow) cycle is misreported as Unhealthy and can flap the node.
    // The budget stays comfortably below ReclaimAfterSeconds (300s) so a genuinely dead loop is still detected.
    private const int MaxBatchSize = 20;                                        // mirrors the spec's default take
    private const int PerNotificationSendBudgetSeconds = 10;                    // generous per-send allowance
    private const int BatchSendBudgetSeconds = MaxBatchSize * PerNotificationSendBudgetSeconds;

    private readonly NotificationDispatcherHealthState _state = state;
    private readonly IOptions<NotificationDispatcherOptions> _options = options;
    private readonly TimeProvider _timeProvider = timeProvider;

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var snapshot = _state.Snapshot();
        var pollSeconds = Math.Max(1, _options.Value.PollIntervalSeconds);
        // poll cadence margin (pollSeconds * StaleMultiplier) PLUS a synchronous-batch send budget.
        var staleAfter = TimeSpan.FromSeconds((pollSeconds * StaleMultiplier) + BatchSendBudgetSeconds);

        var data = new Dictionary<string, object>
        {
            ["lastRunUtc"] = snapshot.LastRunUtc?.ToString("o") ?? "(never)",
            ["pollIntervalSeconds"] = pollSeconds,
            ["staleThresholdSeconds"] = staleAfter.TotalSeconds,
            ["processedSinceStart"] = snapshot.ProcessedSinceStart
        };

        if (snapshot.LastRunUtc is null)
        {
            // Service may still be warming up. Don't fail the host immediately;
            // surface the state so dashboards know the loop hasn't ticked yet.
            return Task.FromResult(HealthCheckResult.Degraded(
                "Notification dispatcher has not completed a cycle yet.",
                data: data));
        }

        var age = _timeProvider.GetUtcNow() - snapshot.LastRunUtc.Value;
        if (age > staleAfter)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"Notification dispatcher has not run for {age.TotalSeconds:F0}s (stale threshold {staleAfter.TotalSeconds:F0}s).",
                data: data));
        }

        if (!snapshot.LastRunSucceeded)
        {
            data["lastError"] = snapshot.LastError ?? "(no error message captured)";
            return Task.FromResult(HealthCheckResult.Degraded(
                "Last notification dispatcher cycle failed.",
                data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            "Notification dispatcher running normally.",
            data: data));
    }
}
