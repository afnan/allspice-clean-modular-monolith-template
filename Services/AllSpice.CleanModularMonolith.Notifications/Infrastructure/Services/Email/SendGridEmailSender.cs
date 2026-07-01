using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Services;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Services.Email;

public sealed class SendGridEmailSender(
    HttpClient httpClient,
    IOptions<SendGridOptions> options,
    ILogger<SendGridEmailSender> logger) : IEmailSender
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly SendGridOptions _options = options.Value;
    private readonly ILogger<SendGridEmailSender> _logger = logger;

    public async Task SendEmailAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        // Build the SendGrid client over the DI-managed HttpClient (registered via AddHttpClient in
        // NotificationsModuleExtensions) rather than newing a SendGridClient(apiKey) per send — the latter
        // creates a fresh internal HttpClient/handler every call, leaking sockets under load. The injected
        // HttpClient's handler is pooled/rotated by IHttpClientFactory, so it is safe to reuse.
        var client = new SendGridClient(_httpClient, _options.ApiKey);

        var envelope = EmailEnvelope.From(message, _options.FromAddress, _options.FromName, _options.ReplyToAddress);

        var from = new EmailAddress(envelope.FromAddress, envelope.FromName);
        var to = new EmailAddress(message.To);

        var msg = MailHelper.CreateSingleEmail(
            from, to, message.Subject, plainTextContent: envelope.TextBody, htmlContent: envelope.HtmlBody);

        if (!string.IsNullOrWhiteSpace(envelope.ReplyToAddress))
        {
            msg.ReplyTo = new EmailAddress(envelope.ReplyToAddress);
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
