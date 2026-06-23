using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Persistence;
using AllSpice.CleanModularMonolith.SharedKernel.Repositories;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Repositories;

public sealed class NotificationRepository : EfRepository<NotificationsDbContext, Notification>, INotificationRepository
{
    public NotificationRepository(NotificationsDbContext dbContext)
        : base(dbContext)
    {
    }
}


