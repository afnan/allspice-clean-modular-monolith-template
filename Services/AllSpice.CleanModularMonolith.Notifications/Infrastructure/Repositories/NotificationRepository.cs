using Ardalis.Specification.EntityFrameworkCore;
using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Persistence;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Repositories;

public sealed class NotificationRepository : RepositoryBase<Notification>, INotificationRepository
{
    public NotificationRepository(NotificationsDbContext dbContext)
        : base(dbContext)
    {
    }
}


