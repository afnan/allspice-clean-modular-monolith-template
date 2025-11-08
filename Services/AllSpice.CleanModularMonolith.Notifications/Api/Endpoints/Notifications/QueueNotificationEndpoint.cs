using FastEndpoints;
using Mediator;
using AllSpice.CleanModularMonolith.Notifications.Application.Features.Notifications.Commands.QueueNotification;
using AllSpice.CleanModularMonolith.Notifications.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace AllSpice.CleanModularMonolith.Notifications.Api.Endpoints.Notifications;

public sealed class QueueNotificationEndpoint : Endpoint<QueueNotificationRequest, QueueNotificationResponse>
{
    private readonly IMediator _mediator;

    public QueueNotificationEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/notifications");
    }

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

public sealed class QueueNotificationRequest
{
    public string RecipientUserId { get; set; } = string.Empty;

    public string? RecipientEmail { get; set; }
        = null;

    public string? RecipientPhoneNumber { get; set; }
        = null;

    public string Channel { get; set; } = NotificationChannel.Email.Name;

    public string Subject { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public string? TemplateKey { get; set; }
        = null;

    public Dictionary<string, string>? Metadata { get; set; }
        = null;

    public DateTimeOffset? ScheduledSendUtc { get; set; }
        = null;

    public string? CorrelationId { get; set; }
        = null;
}

public sealed record QueueNotificationResponse(Guid NotificationId);
public sealed record QueueNotificationErrorResponse(IReadOnlyCollection<string> Errors);


