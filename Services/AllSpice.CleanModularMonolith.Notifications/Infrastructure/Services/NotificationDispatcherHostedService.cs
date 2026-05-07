using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Services;

/// <summary>
/// Background service that periodically dispatches queued notifications.
/// Reports its liveness through <see cref="NotificationDispatcherHealthState"/> so
/// <see cref="NotificationDispatcherHealthCheck"/> can surface a stuck loop.
/// </summary>
public sealed class NotificationDispatcherHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NotificationDispatcherHostedService> _logger;
    private readonly NotificationDispatcherOptions _options;
    private readonly NotificationDispatcherHealthState _health;

    public NotificationDispatcherHostedService(
        IServiceProvider serviceProvider,
        IOptions<NotificationDispatcherOptions> options,
        NotificationDispatcherHealthState health,
        ILogger<NotificationDispatcherHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options.Value;
        _health = health;
    }

    /// <summary>
    /// Executes the dispatch loop until the host is shutting down.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token signaled when the host is stopping.</param>
    /// <returns>A task representing the long-running background operation.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Notification dispatcher hosted service starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _serviceProvider.CreateAsyncScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();

                var processed = await dispatcher.DispatchPendingAsync(stoppingToken);
                _health.RecordSuccess(processed);

                if (processed > 0)
                {
                    _logger.LogInformation("Dispatched {Count} notifications.", processed);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _health.RecordFailure(ex.Message);
                _logger.LogError(ex, "Error occurred while dispatching notifications.");
            }

            // Jitter the delay by ±20% so multiple replicas don't synchronize their DB hits.
            // Random.Shared is thread-safe and seeded per-process, which is fine for jitter
            // (we don't need cryptographic randomness here).
            var baseSeconds = Math.Max(1, _options.PollIntervalSeconds);
            var jitterFactor = 0.8 + (Random.Shared.NextDouble() * 0.4); // 0.8 .. 1.2
            var delay = TimeSpan.FromSeconds(baseSeconds * jitterFactor);
            await Task.Delay(delay, stoppingToken);
        }

        _logger.LogInformation("Notification dispatcher hosted service stopping.");
    }
}
