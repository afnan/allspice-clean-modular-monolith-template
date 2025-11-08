using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using AllSpice.CleanModularMonolith.SharedKernel.Repositories;

namespace AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Persistence;

public interface INotificationRepository : IRepository<Notification>, IReadRepository<Notification>
{
}


