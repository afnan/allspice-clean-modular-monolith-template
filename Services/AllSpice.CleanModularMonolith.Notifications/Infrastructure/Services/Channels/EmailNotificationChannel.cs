using Ardalis.GuardClauses;
using Ardalis.Result;
using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Services;
using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Services.Channels;
using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Notifications.Domain.Enums;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Services.Channels;

public sealed class EmailNotificationChannel : INotificationChannel
{
    private readonly IEmailSender _emailSender;

    public EmailNotificationChannel(IEmailSender emailSender)
    {
        _emailSender = emailSender;
    }

    public NotificationChannel Channel => NotificationChannel.Email;

    public async Task<Result> SendAsync(Notification notification, NotificationContent content, CancellationToken cancellationToken = default)
    {
        Guard.Against.Null(notification);
        Guard.Against.Null(notification.Recipient, nameof(notification.Recipient));
        Guard.Against.NullOrWhiteSpace(notification.Recipient.Email, nameof(notification.Recipient.Email));

        var message = new EmailMessage(
            notification.Recipient.Email,
            content.Subject,
            content.Body,
            content.IsHtml,
            CorrelationId: notification.CorrelationId);

        await _emailSender.SendEmailAsync(message, cancellationToken);

        return Result.Success();
    }
}


