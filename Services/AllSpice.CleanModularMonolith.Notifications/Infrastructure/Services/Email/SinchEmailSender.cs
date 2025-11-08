using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using Ardalis.GuardClauses;
using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Services;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Options;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Services.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Services.Email;

public sealed class SinchEmailSender : IEmailSender
{
    private readonly HttpClient _httpClient;
    private readonly SinchOptions _options;
    private readonly ILogger<SinchEmailSender> _logger;

    public SinchEmailSender(
        HttpClient httpClient,
        IOptions<SinchOptions> options,
        ILogger<SinchEmailSender> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendEmailAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        Guard.Against.Null(message);
        Guard.Against.NullOrWhiteSpace(message.To, nameof(message.To));

        if (!IsConfigured())
        {
            _logger.LogWarning("Sinch email configuration is incomplete. Email to {Recipient} skipped.", message.To);
            return;
        }

        var payload = new SinchEmailRequest(
            Sender: new SinchEmailAddress(_options.Email.FromAddress, message.From ?? _options.Email.FromAddress),
            Recipients: new[] { new SinchEmailAddress(message.To, message.To) },
            Subject: message.Subject,
            HtmlBody: message.IsHtml ? message.Body : null,
            TextBody: message.IsHtml ? null : message.Body,
            ReplyTo: string.IsNullOrWhiteSpace(_options.Email.ReplyToAddress)
                ? null
                : new[] { new SinchEmailAddress(_options.Email.ReplyToAddress, _options.Email.ReplyToAddress) });

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"https://email.api.sinch.com/v1/projects/{_options.ProjectId}/emails:send")
            {
                Content = JsonContent.Create(payload)
            };

            request.Headers.Authorization = CreateAuthorizationHeader(_options.ProjectId, _options.ApiKey);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Sinch email send failed ({StatusCode}): {Error}", response.StatusCode, error);
                throw new InvalidOperationException($"Sinch email send failed: {response.StatusCode}");
            }

            _logger.LogInformation("Sent email notification to {Recipient}", message.To);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email notification to {Recipient}", message.To);
            throw;
        }
    }

    private bool IsConfigured() =>
        !string.IsNullOrWhiteSpace(_options.ProjectId) &&
        !string.IsNullOrWhiteSpace(_options.ApiKey) &&
        !string.IsNullOrWhiteSpace(_options.Email.FromAddress);

    private static System.Net.Http.Headers.AuthenticationHeaderValue CreateAuthorizationHeader(string projectId, string apiKey)
    {
        var credentialBytes = Encoding.ASCII.GetBytes($"{projectId}:{apiKey}");
        return new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(credentialBytes));
    }

    private sealed record SinchEmailRequest(
        [property: JsonPropertyName("from")] SinchEmailAddress Sender,
        [property: JsonPropertyName("to")] IReadOnlyCollection<SinchEmailAddress> Recipients,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("htmlBody")] string? HtmlBody,
        [property: JsonPropertyName("textBody")] string? TextBody,
        [property: JsonPropertyName("replyTo")] IReadOnlyCollection<SinchEmailAddress>? ReplyTo);

    private sealed record SinchEmailAddress(
        [property: JsonPropertyName("email")] string Email,
        [property: JsonPropertyName("name")] string Name);
}
