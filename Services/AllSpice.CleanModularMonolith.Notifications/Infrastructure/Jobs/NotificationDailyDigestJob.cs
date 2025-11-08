using Ardalis.Specification;
using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Notifications.Domain.Enums;
using Microsoft.Extensions.Logging;
using Quartz;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Jobs;

public sealed class NotificationDailyDigestJob : IJob
{
    private readonly INotificationRepository _notificationRepository;
    private readonly ILogger<NotificationDailyDigestJob> _logger;

    public NotificationDailyDigestJob(
        INotificationRepository notificationRepository,
        ILogger<NotificationDailyDigestJob> logger)
    {
        _notificationRepository = notificationRepository;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var cancellationToken = context.CancellationToken;
        var cutoff = DateTimeOffset.UtcNow.AddDays(-1);

        var pendingSpec = new PendingNotificationsSpecification(cutoff);
        var pendingCount = await _notificationRepository.CountAsync(pendingSpec, cancellationToken);

        _logger.LogInformation(
            "Notification daily digest: {PendingCount} pending notifications older than {Cutoff}.",
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


