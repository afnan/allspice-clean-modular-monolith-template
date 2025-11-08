using Ardalis.Specification;
using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Notifications.Domain.Enums;

namespace AllSpice.CleanModularMonolith.Notifications.Domain.Specifications;

public sealed class DueNotificationsSpecification : Specification<Notification>
{
    public DueNotificationsSpecification(DateTimeOffset utcNow, int take = 20)
    {
        Query.Where(notification =>
                notification.Status == NotificationStatus.Pending &&
                notification.AttemptCount < Notification.MaxDeliveryAttempts &&
                (notification.NextAttemptUtc == null || notification.NextAttemptUtc <= utcNow) &&
                ((notification.ScheduledSendUtc ?? notification.CreatedUtc) <= utcNow))
             .OrderBy(notification => notification.CreatedUtc)
             .Take(take);
    }
}


