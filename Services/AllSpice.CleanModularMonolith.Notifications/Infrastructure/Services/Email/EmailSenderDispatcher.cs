using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Services;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Services.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Options;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Services.Email;

public sealed class EmailSenderDispatcher : IEmailSender
{
    private readonly SinchEmailSender _sinchEmailSender;
    private readonly MailKitEmailSender _mailKitEmailSender;
    private readonly SinchOptions _sinchOptions;
    private readonly ILogger<EmailSenderDispatcher> _logger;

    public EmailSenderDispatcher(
        SinchEmailSender sinchEmailSender,
        MailKitEmailSender mailKitEmailSender,
        IOptions<SinchOptions> sinchOptions,
        ILogger<EmailSenderDispatcher> logger)
    {
        _sinchEmailSender = sinchEmailSender;
        _mailKitEmailSender = mailKitEmailSender;
        _sinchOptions = sinchOptions.Value;
        _logger = logger;
    }

    public async Task SendEmailAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (IsSinchConfigured())
        {
            await _sinchEmailSender.SendEmailAsync(message, cancellationToken);
        }
        else
        {
            _logger.LogDebug("Sinch email not configured; falling back to MailKit for {Recipient}.", message.To);
            await _mailKitEmailSender.SendEmailAsync(message, cancellationToken);
        }
    }

    private bool IsSinchConfigured() =>
        !string.IsNullOrWhiteSpace(_sinchOptions.ProjectId) &&
        !string.IsNullOrWhiteSpace(_sinchOptions.ApiKey) &&
        !string.IsNullOrWhiteSpace(_sinchOptions.Email.FromAddress);
}
