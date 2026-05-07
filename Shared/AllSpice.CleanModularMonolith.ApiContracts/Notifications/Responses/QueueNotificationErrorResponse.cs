namespace AllSpice.CleanModularMonolith.ApiContracts.Notifications.Responses;

/// <summary>
/// Response returned when a notification request fails validation or processing.
/// </summary>
public sealed record QueueNotificationErrorResponse(IReadOnlyCollection<string> Errors);
