using Ardalis.Specification;
using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Notifications.Domain.Enums;
using Microsoft.Extensions.Logging;
using Quartz;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Jobs;

/// <summary>
/// Scheduled job that <em>monitors</em> notification delivery health: it counts notifications still
/// <see cref="NotificationStatus.Pending"/> after a cutoff (default 1 day) and logs the backlog so a
/// growing count surfaces a stuck dispatcher. It does NOT send a digest email — despite the original
/// "daily digest" name. Building a real recipient-facing digest is a feature, deliberately out of scope
/// for the template; this job is the lightweight observability stand-in.
/// </summary>
public sealed class StalePendingNotificationsMonitorJob(
    INotificationRepository notificationRepository,
    TimeProvider timeProvider,
    ILogger<StalePendingNotificationsMonitorJob> logger) : IJob
{
    private readonly INotificationRepository _notificationRepository = notificationRepository;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly ILogger<StalePendingNotificationsMonitorJob> _logger = logger;

    public async Task Execute(IJobExecutionContext context)
    {
        var cancellationToken = context.CancellationToken;
        var cutoff = _timeProvider.GetUtcNow().AddDays(-1);

        var pendingSpec = new PendingNotificationsSpecification(cutoff);
        var pendingCount = await _notificationRepository.CountAsync(pendingSpec, cancellationToken);

        _logger.LogInformation(
            "Stale-pending notifications monitor: {PendingCount} notifications still pending older than {Cutoff}.",
            pendingCount,
            cutoff);
    }

    private sealed class PendingNotificationsSpecification : Specification<Notification>
    {
        public PendingNotificationsSpecification(DateTimeOffset cutoff)
        {
            Query.Where(notification =>
                    notification.Status == NotificationStatus.Pending &&
                    notification.CreatedUtc <= cutoff);
        }
    }
}
