using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Persistence;

public interface INotificationsDbContext
{
    DbSet<Notification> Notifications { get; }
    DbSet<NotificationTemplate> NotificationTemplates { get; }
    DbSet<NotificationPreference> NotificationPreferences { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}


