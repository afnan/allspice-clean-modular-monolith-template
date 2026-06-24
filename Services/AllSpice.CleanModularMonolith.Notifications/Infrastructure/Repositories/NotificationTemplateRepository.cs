using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Persistence;
using AllSpice.CleanModularMonolith.SharedKernel.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Repositories;

public sealed class NotificationTemplateRepository(NotificationsDbContext dbContext)
    : EfRepository<NotificationsDbContext, NotificationTemplate>(dbContext), INotificationTemplateRepository
{
    private readonly NotificationsDbContext _dbContext = dbContext;

    public Task<NotificationTemplate?> GetByKeyAsync(string key, CancellationToken cancellationToken = default) =>
        _dbContext.NotificationTemplates.FirstOrDefaultAsync(template => template.Key == key, cancellationToken);
}


