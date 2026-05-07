using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Services;

/// <summary>
/// Reports the health of the notification dispatcher background service.
/// Unhealthy when the service hasn't completed an iteration recently — catches
/// a silently-dead loop that wouldn't otherwise show up in logs or metrics.
/// </summary>
public sealed class NotificationDispatcherHealthCheck : IHealthCheck
{
    private const int StaleMultiplier = 3;

    private readonly NotificationDispatcherHealthState _state;
    private readonly IOptions<NotificationDispatcherOptions> _options;

    public NotificationDispatcherHealthCheck(
        NotificationDispatcherHealthState state,
        IOptions<NotificationDispatcherOptions> options)
    {
        _state = state;
        _options = options;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var (lastRunUtc, lastRunSucceeded, lastError) = _state.Snapshot();
        var pollSeconds = Math.Max(1, _options.Value.PollIntervalSeconds);
        var staleAfter = TimeSpan.FromSeconds(pollSeconds * StaleMultiplier);

        var data = new Dictionary<string, object>
        {
            ["lastRunUtc"] = lastRunUtc?.ToString("o") ?? "(never)",
            ["pollIntervalSeconds"] = pollSeconds,
            ["staleThresholdSeconds"] = staleAfter.TotalSeconds,
            ["processedSinceStart"] = _state.ProcessedSinceStart
        };

        if (lastRunUtc is null)
        {
            // Service may still be warming up. Don't fail the host immediately;
            // surface the state so dashboards know the loop hasn't ticked yet.
            return Task.FromResult(HealthCheckResult.Degraded(
                "Notification dispatcher has not completed a cycle yet.",
                data: data));
        }

        var age = DateTimeOffset.UtcNow - lastRunUtc.Value;
        if (age > staleAfter)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"Notification dispatcher has not run for {age.TotalSeconds:F0}s (stale threshold {staleAfter.TotalSeconds:F0}s).",
                data: data));
        }

        if (!lastRunSucceeded)
        {
            data["lastError"] = lastError ?? "(no error message captured)";
            return Task.FromResult(HealthCheckResult.Degraded(
                "Last notification dispatcher cycle failed.",
                data: data));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            "Notification dispatcher running normally.",
            data: data));
    }
}
