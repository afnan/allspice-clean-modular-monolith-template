using Ardalis.Result;
using AllSpice.CleanModularMonolith.Notifications.Domain.Enums;
using AllSpice.CleanModularMonolith.SharedKernel.Behaviors;
using Mediator;

namespace AllSpice.CleanModularMonolith.Notifications.Application.Features.Preferences.Commands.UpsertNotificationPreference;

/// <summary>
/// Sets the user's opt-in state for a notification channel.
/// </summary>
/// <param name="UserId">Local user UUID (User.Id) — not the Keycloak external ID.</param>
/// <param name="Channel">Notification channel.</param>
/// <param name="IsEnabled">True to enable, false to opt out.</param>
public sealed record UpsertNotificationPreferenceCommand(
    Guid UserId,
    NotificationChannel Channel,
    bool IsEnabled) : IRequest<Result>, ITransactional;
