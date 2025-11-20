using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Services;

/// <summary>
/// Background service that periodically dispatches queued notifications.
/// </summary>
public sealed class NotificationDispatcherHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NotificationDispatcherHostedService> _logger;
    private readonly NotificationDispatcherOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationDispatcherHostedService"/> class.
    /// </summary>
    /// <param name="serviceProvider">Service provider used to resolve scoped dependencies.</param>
    /// <param name="options">Options that configure polling cadence.</param>
    /// <param name="logger">Logger used to record dispatcher activity.</param>
    public NotificationDispatcherHostedService(
        IServiceProvider serviceProvider,
        IOptions<NotificationDispatcherOptions> options,
        ILogger<NotificationDispatcherHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options.Value;
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
                using var scope = _serviceProvider.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();

                var processed = await dispatcher.DispatchPendingAsync(stoppingToken);

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
                _logger.LogError(ex, "Error occurred while dispatching notifications.");
            }

            var delay = TimeSpan.FromSeconds(Math.Max(1, _options.PollIntervalSeconds));
            await Task.Delay(delay, stoppingToken);
        }

        _logger.LogInformation("Notification dispatcher hosted service stopping.");
    }
}


