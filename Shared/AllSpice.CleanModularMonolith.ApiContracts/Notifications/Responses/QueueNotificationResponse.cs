namespace AllSpice.CleanModularMonolith.ApiContracts.Notifications.Responses;

/// <summary>
/// Response returned when a notification is queued successfully.
/// </summary>
public sealed record QueueNotificationResponse(Guid NotificationId);
