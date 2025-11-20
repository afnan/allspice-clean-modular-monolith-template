using System.Text.Json;
using Ardalis.GuardClauses;
using Ardalis.Result;
using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Notifications.Domain.ValueObjects;
using Mediator;

namespace AllSpice.CleanModularMonolith.Notifications.Application.Features.Notifications.Commands.QueueNotification;

/// <summary>
/// Handles requests to queue notifications for asynchronous processing.
/// </summary>
public sealed class QueueNotificationCommandHandler : IRequestHandler<QueueNotificationCommand, Result<Guid>>
{
    private readonly INotificationRepository _notificationRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueueNotificationCommandHandler"/> class.
    /// </summary>
    /// <param name="notificationRepository">Repository used to persist notifications.</param>
    public QueueNotificationCommandHandler(INotificationRepository notificationRepository)
    {
        Guard.Against.Null(notificationRepository);

        _notificationRepository = notificationRepository;
    }

    /// <inheritdoc />
    public async ValueTask<Result<Guid>> Handle(QueueNotificationCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var recipient = NotificationRecipient.Create(
                request.RecipientUserId,
                request.RecipientEmail,
                request.RecipientPhoneNumber);

            var metadataJson = request.Metadata is null
                ? null
                : JsonSerializer.Serialize(request.Metadata);

            var notification = Notification.Queue(
                request.Channel,
                recipient,
                request.Subject,
                request.Body,
                request.TemplateKey,
                metadataJson,
                request.ScheduledSendUtc,
                request.CorrelationId);

            await _notificationRepository.AddAsync(notification, cancellationToken);

            return Result.Success(notification.Id);
        }
        catch (Exception ex)
        {
            return Result.Error(ex.Message);
        }
    }
}


