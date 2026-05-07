namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Services;

/// <summary>
/// Thread-safe state holder updated by <see cref="NotificationDispatcherHostedService"/>
/// after each dispatch cycle and read by <see cref="NotificationDispatcherHealthCheck"/>.
/// </summary>
/// <remarks>
/// Registered as a singleton so the hosted service and the health check share state
/// across requests.
/// </remarks>
public sealed class NotificationDispatcherHealthState
{
    private readonly object _gate = new();
    private DateTimeOffset? _lastRunUtc;
    private bool _lastRunSucceeded;
    private string? _lastError;
    private long _processedSinceStart;

    public void RecordSuccess(int processedCount)
    {
        lock (_gate)
        {
            _lastRunUtc = DateTimeOffset.UtcNow;
            _lastRunSucceeded = true;
            _lastError = null;
            _processedSinceStart += processedCount;
        }
    }

    public void RecordFailure(string error)
    {
        lock (_gate)
        {
            _lastRunUtc = DateTimeOffset.UtcNow;
            _lastRunSucceeded = false;
            _lastError = error;
        }
    }

    public DispatcherSnapshot Snapshot()
    {
        lock (_gate)
        {
            return new DispatcherSnapshot(_lastRunUtc, _lastRunSucceeded, _lastError, _processedSinceStart);
        }
    }
}

/// <summary>
/// Point-in-time snapshot of the dispatcher's health-relevant state. All reads of the
/// underlying mutable fields happen inside the lock so callers see a consistent view.
/// </summary>
public readonly record struct DispatcherSnapshot(
    DateTimeOffset? LastRunUtc,
    bool LastRunSucceeded,
    string? LastError,
    long ProcessedSinceStart);
