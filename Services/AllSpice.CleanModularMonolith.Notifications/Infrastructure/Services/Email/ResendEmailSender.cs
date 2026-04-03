using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Resend;
using AppEmailMessage = AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Services.EmailMessage;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Services.Email;

public sealed class ResendEmailSender : Application.Contracts.Services.IEmailSender
{
    private readonly IResend _resend;
    private readonly ResendOptions _options;
    private readonly ILogger<ResendEmailSender> _logger;

    public ResendEmailSender(
        IResend resend,
        IOptions<ResendOptions> options,
        ILogger<ResendEmailSender> logger)
    {
        _resend = resend;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendEmailAsync(AppEmailMessage message, CancellationToken cancellationToken = default)
    {
        var fromAddress = message.From ?? _options.FromAddress;
        var from = !string.IsNullOrWhiteSpace(_options.FromName)
            ? $"{_options.FromName} <{fromAddress}>"
            : fromAddress;

        var resendMessage = new Resend.EmailMessage
        {
            From = from,
            To = message.To,
            Subject = message.Subject,
            HtmlBody = message.IsHtml ? message.Body : null,
            TextBody = message.IsHtml ? null : message.Body
        };

        if (!string.IsNullOrWhiteSpace(_options.ReplyToAddress))
        {
            resendMessage.ReplyTo = _options.ReplyToAddress;
        }

        await _resend.EmailSendAsync(resendMessage, cancellationToken);

        _logger.LogInformation("Sent email via Resend to {Recipient}", message.To);
    }
}
