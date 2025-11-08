using Ardalis.Result;
using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Notifications.Application.DTOs;
using AllSpice.CleanModularMonolith.Notifications.Domain.Specifications;
using Mediator;

namespace AllSpice.CleanModularMonolith.Notifications.Application.Features.Notifications.Queries.GetUserNotifications;

public sealed class GetUserNotificationsQueryHandler : IRequestHandler<GetUserNotificationsQuery, Result<IReadOnlyCollection<NotificationDto>>>
{
    private readonly INotificationRepository _notificationRepository;

    public GetUserNotificationsQueryHandler(INotificationRepository notificationRepository)
    {
        _notificationRepository = notificationRepository;
    }

    public async ValueTask<Result<IReadOnlyCollection<NotificationDto>>> Handle(GetUserNotificationsQuery request, CancellationToken cancellationToken)
    {
        var specification = new NotificationsByUserSpecification(request.UserId);
        var notifications = await _notificationRepository.ListAsync(specification, cancellationToken);

        var dtoList = notifications
            .Select(notification => new NotificationDto(
                notification.Id,
                notification.Channel.Name,
                notification.Subject,
                notification.Body,
                notification.Recipient.UserId,
                notification.Recipient.Email,
                notification.Recipient.PhoneNumber,
                notification.CreatedUtc,
                notification.ScheduledSendUtc,
                notification.LastUpdatedUtc,
                notification.CorrelationId,
                notification.Status.Name))
            .ToList();

        return Result.Success<IReadOnlyCollection<NotificationDto>>(dtoList);
    }
}


