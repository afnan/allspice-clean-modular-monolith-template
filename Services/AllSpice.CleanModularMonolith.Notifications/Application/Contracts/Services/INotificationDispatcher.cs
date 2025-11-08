namespace AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Services;

public interface INotificationDispatcher
{
    Task<int> DispatchPendingAsync(CancellationToken cancellationToken = default);
}


