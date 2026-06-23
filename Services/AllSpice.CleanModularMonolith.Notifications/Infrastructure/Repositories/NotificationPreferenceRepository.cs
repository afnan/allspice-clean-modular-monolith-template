using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Notifications.Domain.Enums;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Persistence;
using AllSpice.CleanModularMonolith.SharedKernel.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Repositories;

public sealed class NotificationPreferenceRepository : EfRepository<NotificationsDbContext, NotificationPreference>, INotificationPreferenceRepository
{
    private readonly NotificationsDbContext _dbContext;

    public NotificationPreferenceRepository(NotificationsDbContext dbContext)
        : base(dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<NotificationPreference?> GetByUserAndChannelAsync(Guid userId, NotificationChannel channel, CancellationToken cancellationToken = default) =>
        _dbContext.NotificationPreferences.FirstOrDefaultAsync(pref => pref.UserId == userId && pref.Channel == channel, cancellationToken);
}
