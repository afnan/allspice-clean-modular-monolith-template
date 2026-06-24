using AllSpice.CleanModularMonolith.Notifications.Domain.Enums;
using FastEndpoints;
using FluentValidation;

namespace AllSpice.CleanModularMonolith.Notifications.Api.Endpoints.Notifications;

/// <summary>
/// Validates the inbound queue-notification request. Critically, it rejects an unknown <c>Channel</c> name
/// with a 400 <em>before</em> <see cref="QueueNotificationEndpoint"/> resolves the SmartEnum via
/// <c>NotificationChannel.FromName</c> — which would otherwise throw <c>SmartEnumNotFoundException</c> → 500.
/// </summary>
public sealed class QueueNotificationRequestValidator : Validator<QueueNotificationRequest>
{
    public QueueNotificationRequestValidator()
    {
        RuleFor(x => x.Channel)
            .NotEmpty()
            .Must(name => NotificationChannel.TryFromName(name, ignoreCase: true, out _))
            .WithMessage(_ =>
                $"Unknown notification channel. Valid channels: {string.Join(", ", NotificationChannel.List.Select(c => c.Name))}.");
    }
}
