using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Services;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Services.Email;

/// <summary>
/// Dispatches emails through a provider fallback chain with error recovery.
/// Development: always MailKit (Papercut SMTP).
/// Production: Resend -> SendGrid. MailKit is a <em>development-only</em> sender (local/Papercut SMTP), so it
/// is never used as a silent production fallback — that would quietly drop mail to a non-existent local server.
/// In production the dispatcher fails fast when no provider is configured, or when every configured provider fails.
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

        // Production: real providers only (Resend -> SendGrid). Fail fast rather than silently using MailKit.
        var resendConfigured = IsResendConfigured();
        var sendGridConfigured = IsSendGridConfigured();

        if (!resendConfigured && !sendGridConfigured)
        {
            throw new InvalidOperationException(
                "No production email provider is configured. Configure Resend (Notifications:Resend) or " +
                "SendGrid (Notifications:SendGrid) with an API key and from-address, or run in the Development " +
                "environment to use the local SMTP (MailKit/Papercut) sender.");
        }

        if (resendConfigured)
        {
            try
            {
                _logger.LogDebug("Sending email via Resend to {Recipient}", message.To);
                await _resendSender.SendEmailAsync(message, cancellationToken);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Resend failed for {Recipient}, falling back to next provider", message.To);
            }
        }

        if (sendGridConfigured)
        {
            try
            {
                _logger.LogDebug("Sending email via SendGrid to {Recipient}", message.To);
                await _sendGridSender.SendEmailAsync(message, cancellationToken);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SendGrid failed for {Recipient}", message.To);
            }
        }

        // A provider was configured but every attempt failed. Do NOT fall back to MailKit/localhost in production.
        throw new InvalidOperationException(
            "All configured production email providers (Resend/SendGrid) failed to deliver the message.");
    }

    private bool IsResendConfigured() =>
        !string.IsNullOrWhiteSpace(_resendOptions.ApiKey) &&
        !string.IsNullOrWhiteSpace(_resendOptions.FromAddress);

    private bool IsSendGridConfigured() =>
        !string.IsNullOrWhiteSpace(_sendGridOptions.ApiKey) &&
        !string.IsNullOrWhiteSpace(_sendGridOptions.FromAddress);
}
