namespace AllSpice.CleanModularMonolith.SharedKernel.Notifications;

public sealed record NotificationMessage(
    string Subject,
    string Body,
    IReadOnlyCollection<string> Recipients,
    NotificationChannel Channel = NotificationChannel.Email,
    IDictionary<string, string>? Metadata = null);

public enum NotificationChannel
{
    Email,
    Sms,
    Push
}


