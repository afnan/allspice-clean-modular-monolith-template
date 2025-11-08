using Ardalis.Result;
using AllSpice.CleanModularMonolith.Notifications.Domain.Enums;
using Mediator;

namespace AllSpice.CleanModularMonolith.Notifications.Application.Features.Preferences.Commands.UpsertNotificationPreference;

public sealed record UpsertNotificationPreferenceCommand(
    string UserId,
    NotificationChannel Channel,
    bool IsEnabled) : IRequest<Result>;


