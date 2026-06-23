using System.ComponentModel.DataAnnotations;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Services;

/// <summary>
/// Configuration settings for the notification dispatcher background service.
/// </summary>
public sealed class NotificationDispatcherOptions
{
    /// <summary>
    /// Polling interval (in seconds) between dispatch cycles. Minimum 5s — at scale,
    /// 1s polling produces one SELECT per replica per second on the Notifications
    /// table for very little throughput benefit.
    /// </summary>
    [Range(5, int.MaxValue)]
    public int PollIntervalSeconds { get; set; } = 10;

    /// <summary>
    /// How long (in seconds) a notification may sit in <c>Dispatched</c> before it is considered stranded
    /// (the process crashed between marking it Dispatched and sending) and is reclaimed for another attempt.
    /// Must comfortably exceed the worst-case channel send time to avoid reclaiming an in-flight send.
    /// </summary>
    [Range(30, int.MaxValue)]
    public int ReclaimAfterSeconds { get; set; } = 300;
}


