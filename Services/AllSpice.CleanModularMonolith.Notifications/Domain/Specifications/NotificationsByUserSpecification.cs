using Ardalis.Specification;
using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;

namespace AllSpice.CleanModularMonolith.Notifications.Domain.Specifications;

public sealed class NotificationsByUserSpecification : Specification<Notification>
{
    public NotificationsByUserSpecification(string userId)
    {
        Query.Where(notification => notification.Recipient.UserId == userId)
             .OrderByDescending(notification => notification.CreatedUtc);
    }
}


