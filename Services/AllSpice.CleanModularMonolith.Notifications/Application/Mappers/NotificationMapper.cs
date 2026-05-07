using AllSpice.CleanModularMonolith.ApiContracts.Notifications.Responses;
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

    public static NotificationResponse ToResponse(NotificationDto dto) => new(
        dto.Id,
        dto.Channel,
        dto.Subject,
        dto.Body,
        dto.RecipientUserId,
        dto.RecipientEmail,
        dto.RecipientPhoneNumber,
        dto.CreatedUtc,
        dto.ScheduledSendUtc,
        dto.LastUpdatedUtc,
        dto.CorrelationId,
        dto.Status);
}
