using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using AllSpice.CleanModularMonolith.SharedKernel.Repositories;

namespace AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Persistence;

public interface INotificationTemplateRepository : IRepository<NotificationTemplate>, IReadRepository<NotificationTemplate>
{
    Task<NotificationTemplate?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);
}


