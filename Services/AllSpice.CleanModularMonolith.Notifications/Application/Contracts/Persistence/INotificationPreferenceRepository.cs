using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Notifications.Domain.Enums;
using AllSpice.CleanModularMonolith.SharedKernel.Repositories;

namespace AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Persistence;

public interface INotificationPreferenceRepository : IRepository<NotificationPreference>, IReadRepository<NotificationPreference>
{
    Task<NotificationPreference?> GetByUserAndChannelAsync(string userId, NotificationChannel channel, CancellationToken cancellationToken = default);
}


