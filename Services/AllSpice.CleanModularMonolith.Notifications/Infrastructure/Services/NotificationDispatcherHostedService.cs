using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Services;

public sealed class NotificationDispatcherHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NotificationDispatcherHostedService> _logger;
    private readonly NotificationDispatcherOptions _options;

    public NotificationDispatcherHostedService(
        IServiceProvider serviceProvider,
        IOptions<NotificationDispatcherOptions> options,
        ILogger<NotificationDispatcherHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options.Value;
    }

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


