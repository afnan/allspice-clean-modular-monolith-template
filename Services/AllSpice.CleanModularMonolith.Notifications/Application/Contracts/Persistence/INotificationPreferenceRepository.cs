using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Notifications.Domain.Enums;
using AllSpice.CleanModularMonolith.SharedKernel.Repositories;

namespace AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Persistence;

public interface INotificationPreferenceRepository : IRepository<NotificationPreference>, IReadRepository<NotificationPreference>
{
    /// <summary>
    /// Looks up a preference by local user UUID + channel.
    /// </summary>
    /// <param name="userId">Local user UUID (User.Id) — not the Keycloak external ID.</param>
    /// <param name="channel">Notification channel.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<NotificationPreference?> GetByUserAndChannelAsync(Guid userId, NotificationChannel channel, CancellationToken cancellationToken = default);
}
