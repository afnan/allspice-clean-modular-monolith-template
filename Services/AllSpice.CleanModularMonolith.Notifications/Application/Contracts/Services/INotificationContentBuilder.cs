using Ardalis.Result;
using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;

namespace AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Services;

public interface INotificationContentBuilder
{
    Task<Result<NotificationContent>> BuildAsync(Notification notification, CancellationToken cancellationToken = default);
}

public sealed record NotificationContent(string Subject, string Body, bool IsHtml);


