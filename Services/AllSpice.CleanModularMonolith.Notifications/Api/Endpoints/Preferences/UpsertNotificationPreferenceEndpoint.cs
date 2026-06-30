using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using AllSpice.CleanModularMonolith.Notifications.Application.Features.Preferences.Commands.UpsertNotificationPreference;
using AllSpice.CleanModularMonolith.Notifications.Domain.Enums;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace AllSpice.CleanModularMonolith.Notifications.Api.Endpoints.Preferences;

/// <summary>
/// Sets a user's opt-in state for a notification channel.
/// Gated with <c>notifications:preferences.manage</c> — the worked example of a module
/// permission key enforced at the API boundary (see <see cref="NotificationsPermissionManifest"/>).
/// </summary>
public sealed class UpsertNotificationPreferenceEndpoint(IMediator mediator)
    : Endpoint<UpsertNotificationPreferenceRequest>
{
    private readonly IMediator _mediator = mediator;

    /// <inheritdoc />
    public override void Configure()
    {
        Put("/api/notifications/preferences");
        Policies(PermissionPolicy.For("notifications:preferences.manage"));
        Tags("Notifications");
        Summary(summary =>
        {
            summary.Summary = "Sets a user's opt-in or opt-out state for a notification channel.";
        });
    }

    /// <inheritdoc />
    public override async Task HandleAsync(UpsertNotificationPreferenceRequest req, CancellationToken ct)
    {
        if (!NotificationChannel.TryFromName(req.Channel, ignoreCase: true, out var channel))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(
                new { errors = new[] { $"Unknown notification channel '{req.Channel}'." } },
                cancellationToken: ct);
            return;
        }

        var command = new UpsertNotificationPreferenceCommand(req.UserId, channel, req.IsEnabled);
        var result = await _mediator.Send(command, ct);

        if (result.IsSuccess)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status204NoContent;
            return;
        }

        HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        await HttpContext.Response.WriteAsJsonAsync(
            new { errors = result.Errors.ToArray() },
            cancellationToken: ct);
    }
}

/// <summary>
/// Request payload for upserting a notification preference.
/// </summary>
public sealed class UpsertNotificationPreferenceRequest
{
    /// <summary>Local user UUID (User.Id — not the Keycloak external ID).</summary>
    public Guid UserId { get; set; }

    /// <summary>Notification channel name (e.g. Email, InApp).</summary>
    public string Channel { get; set; } = string.Empty;

    /// <summary>True to opt in; false to opt out.</summary>
    public bool IsEnabled { get; set; }
}
