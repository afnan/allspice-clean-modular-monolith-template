using Ardalis.Result;
using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Notifications.Domain.Enums;

namespace AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Services.Channels;

public interface INotificationChannel
{
    NotificationChannel Channel { get; }

    /// <summary>
    /// Sends the notification through this channel.
    /// <para>
    /// IDEMPOTENCY CONTRACT: dispatch is <em>at-least-once</em>. If a crash lands after a successful send but
    /// before the dispatcher records <c>Delivered</c>, the reclaim path re-invokes <c>SendAsync</c> for the
    /// same notification (on a later attempt). Implementations SHOULD therefore de-duplicate on the stable
    /// <see cref="Notification.Id"/> — e.g. pass it as the provider's idempotency key / message id — so a
    /// re-send does not deliver a second copy. <see cref="Notification.AttemptCount"/> changes across retries
    /// and must NOT be part of the dedup key.
    /// </para>
    /// </summary>
    Task<Result> SendAsync(Notification notification, NotificationContent content, CancellationToken cancellationToken = default);
}


