using Ardalis.Specification.EntityFrameworkCore;
using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Repositories;

public sealed class NotificationTemplateRepository : RepositoryBase<NotificationTemplate>, INotificationTemplateRepository
{
    private readonly NotificationsDbContext _dbContext;

    public NotificationTemplateRepository(NotificationsDbContext dbContext)
        : base(dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<NotificationTemplate?> GetByKeyAsync(string key, CancellationToken cancellationToken = default) =>
        _dbContext.NotificationTemplates.FirstOrDefaultAsync(template => template.Key == key, cancellationToken);
}


