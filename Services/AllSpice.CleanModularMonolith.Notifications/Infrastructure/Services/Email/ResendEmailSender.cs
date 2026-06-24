using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Resend;
using AppEmailMessage = AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Services.EmailMessage;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Services.Email;

public sealed class ResendEmailSender(
    IResend resend,
    IOptions<ResendOptions> options,
    ILogger<ResendEmailSender> logger) : Application.Contracts.Services.IEmailSender
{
    private readonly IResend _resend = resend;
    private readonly ResendOptions _options = options.Value;
    private readonly ILogger<ResendEmailSender> _logger = logger;

    public async Task SendEmailAsync(AppEmailMessage message, CancellationToken cancellationToken = default)
    {
        var envelope = EmailEnvelope.From(message, _options.FromAddress, _options.FromName, _options.ReplyToAddress);

        var from = !string.IsNullOrWhiteSpace(envelope.FromName)
            ? $"{envelope.FromName} <{envelope.FromAddress}>"
            : envelope.FromAddress;

        var resendMessage = new Resend.EmailMessage
        {
            From = from,
            To = message.To,
            Subject = message.Subject,
            HtmlBody = envelope.HtmlBody,
            TextBody = envelope.TextBody
        };

        if (!string.IsNullOrWhiteSpace(envelope.ReplyToAddress))
        {
            resendMessage.ReplyTo = envelope.ReplyToAddress;
        }

        await _resend.EmailSendAsync(resendMessage, cancellationToken);

        _logger.LogInformation("Sent email via Resend to {Recipient}", message.To);
    }
}
