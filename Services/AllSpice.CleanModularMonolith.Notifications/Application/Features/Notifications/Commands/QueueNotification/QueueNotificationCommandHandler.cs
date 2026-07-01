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
public sealed class QueueNotificationCommandHandler(
    INotificationRepository notificationRepository,
    TimeProvider timeProvider)
    : IRequestHandler<QueueNotificationCommand, Result<Guid>>
{
    private readonly INotificationRepository _notificationRepository = Guard.Against.Null(notificationRepository);
    private readonly TimeProvider _timeProvider = Guard.Against.Null(timeProvider);

    /// <inheritdoc />
    public async ValueTask<Result<Guid>> Handle(QueueNotificationCommand request, CancellationToken cancellationToken)
    {
        // No blanket try/catch: invalid input is thrown by ValidationBehavior and mapped to Result.Invalid by
        // DomainExceptionBehavior (QueueNotificationCommandValidator mirrors the domain guards). If either
        // behavior is removed/reordered this safety net breaks. Genuine infrastructure faults
        // must propagate so the integration-event consumer can classify them as transient and retry —
        // swallowing everything into Result.Error both leaked internal messages and mislabelled permanent
        // failures as retryable.
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
            _timeProvider.GetUtcNow(),
            request.ScheduledSendUtc,
            request.CorrelationId);

        await _notificationRepository.AddAsync(notification, cancellationToken);

        return Result.Success(notification.Id);
    }
}


