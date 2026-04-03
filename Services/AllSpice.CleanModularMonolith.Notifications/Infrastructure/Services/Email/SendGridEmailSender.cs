using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Services;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Services.Email;

public sealed class SendGridEmailSender : IEmailSender
{
    private readonly SendGridOptions _options;
    private readonly ILogger<SendGridEmailSender> _logger;

    public SendGridEmailSender(
        IOptions<SendGridOptions> options,
        ILogger<SendGridEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendEmailAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var client = new SendGridClient(_options.ApiKey);

        var fromAddress = message.From ?? _options.FromAddress;
        var from = new EmailAddress(fromAddress, _options.FromName);
        var to = new EmailAddress(message.To);

        var msg = message.IsHtml
            ? MailHelper.CreateSingleEmail(from, to, message.Subject, plainTextContent: null, htmlContent: message.Body)
            : MailHelper.CreateSingleEmail(from, to, message.Subject, plainTextContent: message.Body, htmlContent: null);

        if (!string.IsNullOrWhiteSpace(_options.ReplyToAddress))
        {
            msg.ReplyTo = new EmailAddress(_options.ReplyToAddress);
        }

        var response = await client.SendEmailAsync(msg, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Body.ReadAsStringAsync(cancellationToken);
            _logger.LogError("SendGrid email failed ({StatusCode}): {Body}", response.StatusCode, body);
            throw new InvalidOperationException($"SendGrid email send failed: {response.StatusCode}");
        }

        _logger.LogInformation("Sent email via SendGrid to {Recipient}", message.To);
    }
}
