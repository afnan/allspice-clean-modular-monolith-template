using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Services;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Services.Email;

public sealed class MailKitEmailSender : IEmailSender
{
    private readonly IOptionsMonitor<MailKitSmtpOptions> _optionsMonitor;
    private readonly ILogger<MailKitEmailSender> _logger;

    public MailKitEmailSender(
        IOptionsMonitor<MailKitSmtpOptions> optionsMonitor,
        ILogger<MailKitEmailSender> logger)
    {
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    public async Task SendEmailAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var options = _optionsMonitor.CurrentValue;

        var mimeMessage = new MimeMessage
        {
            Subject = message.Subject,
        };

        var fromAddress = message.From ?? options.FromAddress;
        mimeMessage.From.Add(new MailboxAddress(options.FromName, fromAddress));
        mimeMessage.To.Add(MailboxAddress.Parse(message.To));

        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = message.IsHtml ? message.Body : null,
            TextBody = message.IsHtml ? null : message.Body
        };

        mimeMessage.Body = bodyBuilder.ToMessageBody();

        using var client = new SmtpClient();

        try
        {
            var secureSocketOptions = options.UseSsl
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTlsWhenAvailable;

            await client.ConnectAsync(options.Host, options.Port, secureSocketOptions, cancellationToken);

            if (!string.IsNullOrWhiteSpace(options.Username) && !string.IsNullOrWhiteSpace(options.Password))
            {
                await client.AuthenticateAsync(options.Username, options.Password, cancellationToken);
            }

            await client.SendAsync(mimeMessage, cancellationToken);

            _logger.LogInformation(
                "Sent email to {Recipient} with subject {Subject}.",
                message.To,
                message.Subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Recipient}.", message.To);
            throw;
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(true, cancellationToken);
            }
        }
    }
}
