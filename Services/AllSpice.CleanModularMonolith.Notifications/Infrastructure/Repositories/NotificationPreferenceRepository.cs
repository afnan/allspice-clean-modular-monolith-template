using Ardalis.Specification.EntityFrameworkCore;
using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Notifications.Domain.Enums;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Repositories;

public sealed class NotificationPreferenceRepository : RepositoryBase<NotificationPreference>, INotificationPreferenceRepository
{
    private readonly NotificationsDbContext _dbContext;

    public NotificationPreferenceRepository(NotificationsDbContext dbContext)
        : base(dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<NotificationPreference?> GetByUserAndChannelAsync(string userId, NotificationChannel channel, CancellationToken cancellationToken = default) =>
        _dbContext.NotificationPreferences.FirstOrDefaultAsync(pref => pref.UserId == userId && pref.Channel == channel, cancellationToken);
}


