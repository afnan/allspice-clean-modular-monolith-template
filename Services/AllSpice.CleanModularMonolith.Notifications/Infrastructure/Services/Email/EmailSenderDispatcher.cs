using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Services;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Services.Email;

/// <summary>
/// Dispatches emails through a provider fallback chain.
/// Development: always MailKit (Papercut SMTP).
/// Production: Resend -> SendGrid -> MailKit.
/// </summary>
public sealed class EmailSenderDispatcher : IEmailSender
{
    private readonly IHostEnvironment _environment;
    private readonly ResendEmailSender _resendSender;
    private readonly SendGridEmailSender _sendGridSender;
    private readonly MailKitEmailSender _mailKitSender;
    private readonly ResendOptions _resendOptions;
    private readonly SendGridOptions _sendGridOptions;
    private readonly ILogger<EmailSenderDispatcher> _logger;

    public EmailSenderDispatcher(
        IHostEnvironment environment,
        ResendEmailSender resendSender,
        SendGridEmailSender sendGridSender,
        MailKitEmailSender mailKitSender,
        IOptions<ResendOptions> resendOptions,
        IOptions<SendGridOptions> sendGridOptions,
        ILogger<EmailSenderDispatcher> logger)
    {
        _environment = environment;
        _resendSender = resendSender;
        _sendGridSender = sendGridSender;
        _mailKitSender = mailKitSender;
        _resendOptions = resendOptions.Value;
        _sendGridOptions = sendGridOptions.Value;
        _logger = logger;
    }

    public async Task SendEmailAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        // In development, always use MailKit (Papercut SMTP)
        if (_environment.IsDevelopment())
        {
            _logger.LogDebug("Development mode: sending email via MailKit to {Recipient}", message.To);
            await _mailKitSender.SendEmailAsync(message, cancellationToken);
            return;
        }

        // Production fallback chain: Resend -> SendGrid -> MailKit
        if (IsResendConfigured())
        {
            _logger.LogDebug("Sending email via Resend to {Recipient}", message.To);
            await _resendSender.SendEmailAsync(message, cancellationToken);
            return;
        }

        if (IsSendGridConfigured())
        {
            _logger.LogDebug("Sending email via SendGrid to {Recipient}", message.To);
            await _sendGridSender.SendEmailAsync(message, cancellationToken);
            return;
        }

        _logger.LogDebug("No cloud email provider configured; falling back to MailKit for {Recipient}", message.To);
        await _mailKitSender.SendEmailAsync(message, cancellationToken);
    }

    private bool IsResendConfigured() =>
        !string.IsNullOrWhiteSpace(_resendOptions.ApiKey) &&
        !string.IsNullOrWhiteSpace(_resendOptions.FromAddress);

    private bool IsSendGridConfigured() =>
        !string.IsNullOrWhiteSpace(_sendGridOptions.ApiKey) &&
        !string.IsNullOrWhiteSpace(_sendGridOptions.FromAddress);
}
