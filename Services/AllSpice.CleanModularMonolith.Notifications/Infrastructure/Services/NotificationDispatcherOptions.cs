namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Services;

/// <summary>
/// Configuration settings for the notification dispatcher background service.
/// </summary>
public sealed class NotificationDispatcherOptions
{
    /// <summary>
    /// Gets or sets the polling interval (in seconds) between dispatch cycles.
    /// </summary>
    public int PollIntervalSeconds { get; set; } = 10;
}


