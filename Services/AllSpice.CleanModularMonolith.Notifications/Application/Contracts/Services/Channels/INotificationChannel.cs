using Ardalis.Result;
using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Notifications.Domain.Enums;

namespace AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Services.Channels;

public interface INotificationChannel
{
    NotificationChannel Channel { get; }

    Task<Result> SendAsync(Notification notification, NotificationContent content, CancellationToken cancellationToken = default);
}


