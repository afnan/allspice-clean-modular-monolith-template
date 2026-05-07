using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Notifications.Domain.Enums;
using Ardalis.Specification;

namespace AllSpice.CleanModularMonolith.Notifications.Domain.Specifications;

public sealed class NotificationPreferenceByUserAndChannelSpec : Specification<NotificationPreference>, ISingleResultSpecification<NotificationPreference>
{
    /// <summary>
    /// Filters by local user UUID + channel.
    /// </summary>
    /// <param name="userId">Local user UUID (User.Id) — not the Keycloak external ID.</param>
    /// <param name="channel">Notification channel.</param>
    public NotificationPreferenceByUserAndChannelSpec(Guid userId, NotificationChannel channel)
    {
        Query.Where(p => p.UserId == userId && p.Channel == channel);
    }
}
