using FastEndpoints;
using Mediator;
using AllSpice.CleanModularMonolith.Notifications.Application.Features.Notifications.Commands.QueueNotification;
using AllSpice.CleanModularMonolith.Notifications.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace AllSpice.CleanModularMonolith.Notifications.Api.Endpoints.Notifications;

/// <summary>
/// API endpoint that queues notifications for asynchronous dispatch.
/// </summary>
public sealed class QueueNotificationEndpoint : Endpoint<QueueNotificationRequest, QueueNotificationResponse>
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueueNotificationEndpoint"/> class.
    /// </summary>
    /// <param name="mediator">Mediator used to dispatch module commands.</param>
    public QueueNotificationEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <inheritdoc />
    public override void Configure()
    {
        Post("/api/notifications");
    }

    /// <inheritdoc />
    public override async Task HandleAsync(QueueNotificationRequest req, CancellationToken ct)
    {
        var channel = NotificationChannel.FromName(req.Channel, ignoreCase: true);

        var command = new QueueNotificationCommand(
            req.RecipientUserId,
            req.RecipientEmail,
            req.RecipientPhoneNumber,
            channel,
            req.Subject,
            req.Body,
            req.TemplateKey,
            req.Metadata,
            req.ScheduledSendUtc,
            req.CorrelationId);

        var result = await _mediator.Send(command, ct);

        if (result.IsSuccess)
        {
            await HttpContext.Response.WriteAsJsonAsync(new QueueNotificationResponse(result.Value), cancellationToken: ct);
            return;
        }

        HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        await HttpContext.Response.WriteAsJsonAsync(new QueueNotificationErrorResponse(result.Errors.ToArray()), cancellationToken: ct);
    }
}

/// <summary>
/// Client request payload for queuing notifications.
/// </summary>
public sealed class QueueNotificationRequest
{
    /// <summary>Identifier of the target user inside the notifications domain.</summary>
    public string RecipientUserId { get; set; } = string.Empty;

    /// <summary>Optional email address for email notifications.</summary>
    public string? RecipientEmail { get; set; }
        = null;

    /// <summary>Optional phone number for SMS notifications.</summary>
    public string? RecipientPhoneNumber { get; set; }
        = null;

    /// <summary>Notification channel name (e.g. Email, Sms, InApp).</summary>
    public string Channel { get; set; } = NotificationChannel.Email.Name;

    /// <summary>Notification subject or title.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>Notification body rendered with optional template tokens.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>Optional template key to use when generating the notification.</summary>
    public string? TemplateKey { get; set; }
        = null;

    /// <summary>Arbitrary metadata that accompanies the notification.</summary>
    public Dictionary<string, string>? Metadata { get; set; }
        = null;

    /// <summary>Optional scheduled send time in UTC.</summary>
    public DateTimeOffset? ScheduledSendUtc { get; set; }
        = null;

    /// <summary>Correlation identifier supplied by the caller.</summary>
    public string? CorrelationId { get; set; }
        = null;
}

/// <summary>
/// Response returned when a notification is queued successfully.
/// </summary>
public sealed record QueueNotificationResponse(Guid NotificationId);

/// <summary>
/// Response returned when a notification request fails validation or processing.
/// </summary>
public sealed record QueueNotificationErrorResponse(IReadOnlyCollection<string> Errors);


