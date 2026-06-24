namespace AllSpice.CleanModularMonolith.SharedKernel.Behaviors;

/// <summary>
/// Options for <see cref="PerformanceBehavior{TRequest,TResponse}"/>. Bind from the <c>Performance</c>
/// configuration section to tune the slow-request threshold per environment.
/// </summary>
public sealed class PerformanceBehaviorOptions
{
    /// <summary>
    /// Requests taking at least this many milliseconds are logged as a <c>Warning</c> (so slow requests
    /// surface in production logs); faster requests are logged at <c>Trace</c>. Default: 500 ms.
    /// </summary>
    public long SlowRequestThresholdMs { get; set; } = 500;
}
