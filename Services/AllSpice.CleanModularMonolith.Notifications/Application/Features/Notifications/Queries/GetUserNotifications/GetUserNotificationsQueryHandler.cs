using Ardalis.Result;
using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Notifications.Application.DTOs;
using AllSpice.CleanModularMonolith.Notifications.Application.Mappers;
using AllSpice.CleanModularMonolith.Notifications.Domain.Specifications;
using Mediator;

namespace AllSpice.CleanModularMonolith.Notifications.Application.Features.Notifications.Queries.GetUserNotifications;

public sealed class GetUserNotificationsQueryHandler(INotificationRepository notificationRepository)
    : IRequestHandler<GetUserNotificationsQuery, Result<IReadOnlyCollection<NotificationDto>>>
{
    private readonly INotificationRepository _notificationRepository = notificationRepository;

    public async ValueTask<Result<IReadOnlyCollection<NotificationDto>>> Handle(GetUserNotificationsQuery request, CancellationToken cancellationToken)
    {
        var specification = new NotificationsByUserSpecification(request.UserId);
        var notifications = await _notificationRepository.ListAsync(specification, cancellationToken);

        var dtoList = notifications
            .Select(NotificationMapper.ToDto)
            .ToList();

        return Result.Success<IReadOnlyCollection<NotificationDto>>(dtoList);
    }
}


