using AllSpice.CleanModularMonolith.Notifications.Application.DTOs;
using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;

namespace AllSpice.CleanModularMonolith.Notifications.Application.Mappers;

public static class NotificationMapper
{
    public static NotificationDto ToDto(Notification notification) => new(
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
        notification.Status.Name);
}
