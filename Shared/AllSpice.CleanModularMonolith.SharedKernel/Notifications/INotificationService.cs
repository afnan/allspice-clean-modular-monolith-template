namespace AllSpice.CleanModularMonolith.SharedKernel.Notifications;

public interface INotificationService
{
    Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default);
}


