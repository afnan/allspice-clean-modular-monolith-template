using System.Net.Http.Json;
using System.Text;
using Ardalis.GuardClauses;
using Ardalis.Result;
using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Services;
using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Services.Channels;
using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Notifications.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Options;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Services.Channels;

public sealed class SinchSmsNotificationChannel : INotificationChannel
{
    private readonly HttpClient _httpClient;
    private readonly SinchOptions _options;
    private readonly ILogger<SinchSmsNotificationChannel> _logger;

    public SinchSmsNotificationChannel(
        HttpClient httpClient,
        IOptions<SinchOptions> options,
        ILogger<SinchSmsNotificationChannel> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public NotificationChannel Channel => NotificationChannel.Sms;

    public async Task<Result> SendAsync(Notification notification, NotificationContent content, CancellationToken cancellationToken = default)
    {
        Guard.Against.Null(notification);
        Guard.Against.Null(notification.Recipient, nameof(notification.Recipient));
        Guard.Against.NullOrWhiteSpace(notification.Recipient.PhoneNumber, nameof(notification.Recipient.PhoneNumber));

        if (!IsConfigured())
        {
            _logger.LogWarning("Sinch SMS configuration is incomplete. Unable to send notification {NotificationId}.", notification.Id);
            return Result.Error("Sinch SMS configuration incomplete.");
        }

        var payload = new
        {
            from = _options.Sms.FromNumber,
            to = new[] { notification.Recipient.PhoneNumber },
            body = BuildMessageBody(content)
        };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"https://sms.api.sinch.com/xms/v1/{_options.Sms.ServicePlanId}/batches")
            {
                Content = JsonContent.Create(payload)
            };

            request.Headers.Authorization = CreateAuthorizationHeader(_options.ProjectId, _options.ApiKey);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Sinch SMS send failed ({StatusCode}): {Error}", response.StatusCode, errorContent);
                return Result.Error($"Sinch error: {response.StatusCode}");
            }

            _logger.LogInformation("Sent SMS notification {NotificationId} via Sinch.", notification.Id);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while sending SMS notification {NotificationId}.", notification.Id);
            return Result.Error(ex.Message);
        }
    }

    private bool IsConfigured() =>
        !string.IsNullOrWhiteSpace(_options.ProjectId) &&
        !string.IsNullOrWhiteSpace(_options.ApiKey) &&
        !string.IsNullOrWhiteSpace(_options.Sms.ServicePlanId) &&
        !string.IsNullOrWhiteSpace(_options.Sms.FromNumber);

    private static string BuildMessageBody(NotificationContent content)
    {
        if (!string.IsNullOrWhiteSpace(content.Subject))
        {
            return content.Subject + "\n\n" + content.Body;
        }

        return content.Body;
    }

    private static System.Net.Http.Headers.AuthenticationHeaderValue CreateAuthorizationHeader(string projectId, string apiKey)
    {
        var credentialBytes = Encoding.ASCII.GetBytes($"{projectId}:{apiKey}");
        return new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(credentialBytes));
    }
}


