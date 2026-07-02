using AllSpice.CleanModularMonolith.ApiContracts.Notifications.Responses;
using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using AllSpice.CleanModularMonolith.Notifications.Application.Features.Notifications.Commands.QueueNotification;
using AllSpice.CleanModularMonolith.Notifications.Domain.Enums;
using AllSpice.CleanModularMonolith.Web;
using FastEndpoints;
using Mediator;
using Microsoft.AspNetCore.Http;

namespace AllSpice.CleanModularMonolith.Notifications.Api.Endpoints.Notifications;

/// <summary>
/// API endpoint that queues notifications for asynchronous dispatch.
/// Gated on <c>notifications.access</c> — queuing a notification is a privileged action, so it must
/// never be reachable anonymously (before an IdP is configured the gateway sets no fallback policy).
/// Tighten to a dedicated send permission if send authority should be separated from module access.
/// </summary>
public sealed class QueueNotificationEndpoint(IMediator mediator)
    : Endpoint<QueueNotificationRequest, QueueNotificationResponse>
{
    private readonly IMediator _mediator = mediator;

    /// <inheritdoc />
    public override void Configure()
    {
        Post("/api/notifications");
        Policies(PermissionPolicy.For("notifications.access"));
        Tags("Notifications");
    }

    /// <inheritdoc />
    public override async Task HandleAsync(QueueNotificationRequest req, CancellationToken ct)
    {
        // QueueNotificationRequestValidator already rejects an unknown channel with a 400; this is the
        // defensive fallback so an unrecognized channel never reaches FromName (which would throw → 500).
        if (!NotificationChannel.TryFromName(req.Channel, ignoreCase: true, out var channel))
        {
            await TypedResults.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        ["Channel"] = [$"Unknown notification channel '{req.Channel}'."]
                    })
                .ExecuteAsync(HttpContext);
            return;
        }

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
            await TypedResults.Ok(new QueueNotificationResponse(result.Value)).ExecuteAsync(HttpContext);
            return;
        }

        await result.ExecuteFailureAsync(HttpContext);
    }
}
