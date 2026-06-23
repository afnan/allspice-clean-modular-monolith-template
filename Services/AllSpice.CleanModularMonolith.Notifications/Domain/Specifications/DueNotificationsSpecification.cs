using Ardalis.Specification;
using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Notifications.Domain.Enums;

namespace AllSpice.CleanModularMonolith.Notifications.Domain.Specifications;

public sealed class DueNotificationsSpecification : Specification<Notification>
{
    /// <param name="utcNow">Current time.</param>
    /// <param name="reclaimBefore">
    /// Cutoff for reclaiming stranded <c>Dispatched</c> rows: a notification marked <c>Dispatched</c> whose
    /// last update is at or before this time is assumed orphaned (the process crashed between marking it
    /// Dispatched and sending) and is re-selected. For a <c>Dispatched</c> row, <c>LastUpdatedUtc</c> is the
    /// dispatch time, because nothing mutates the row again until it reaches a terminal state.
    /// </param>
    /// <param name="take">Maximum batch size.</param>
    public DueNotificationsSpecification(DateTimeOffset utcNow, DateTimeOffset reclaimBefore, int take = 20)
    {
        Query.Where(notification =>
                notification.AttemptCount < Notification.MaxDeliveryAttempts &&
                (
                    // Normal: pending and due.
                    (notification.Status == NotificationStatus.Pending &&
                     (notification.NextAttemptUtc == null || notification.NextAttemptUtc <= utcNow) &&
                     ((notification.ScheduledSendUtc ?? notification.CreatedUtc) <= utcNow))
                    ||
                    // Reclaim: stranded in Dispatched past the reclaim cutoff.
                    (notification.Status == NotificationStatus.Dispatched &&
                     notification.LastUpdatedUtc != null &&
                     notification.LastUpdatedUtc <= reclaimBefore)
                ))
             .OrderBy(notification => notification.CreatedUtc)
             .Take(take);
    }
}


