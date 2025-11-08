using Ardalis.Result;
using AllSpice.CleanModularMonolith.Notifications.Domain.Enums;
using Mediator;

namespace AllSpice.CleanModularMonolith.Notifications.Application.Features.Notifications.Commands.QueueNotification;

public sealed record QueueNotificationCommand(
    string RecipientUserId,
    string? RecipientEmail,
    string? RecipientPhoneNumber,
    NotificationChannel Channel,
    string Subject,
    string Body,
    string? TemplateKey,
    IReadOnlyDictionary<string, string>? Metadata,
    DateTimeOffset? ScheduledSendUtc,
    string? CorrelationId) : IRequest<Result<Guid>>;


