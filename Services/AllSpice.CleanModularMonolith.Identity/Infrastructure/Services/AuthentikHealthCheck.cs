using System.Net.Http.Headers;
using System.Text.Json;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Services;

/// <summary>
/// Health check that verifies connectivity to the backing Authentik instance.
/// </summary>
public sealed class AuthentikHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AuthentikHealthCheck> _logger;

    private static readonly Uri HealthEndpoint = new("/api/v3/core/users/?limit=1", UriKind.Relative);

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthentikHealthCheck"/> class.
    /// </summary>
    public AuthentikHealthCheck(
        IHttpClientFactory httpClientFactory,
        ILogger<AuthentikHealthCheck> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient(IdentityModuleExtensions.AuthentikHttpClientName);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, HealthEndpoint);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await client.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy("Authenticated request to Authentik succeeded.");
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "Authentik health request failed with status {Status}. Response: {ResponseBody}",
                response.StatusCode,
                body);

            return HealthCheckResult.Unhealthy(
                $"Authentik responded with status {(int)response.StatusCode}",
                data: new Dictionary<string, object>
                {
                    ["statusCode"] = (int)response.StatusCode,
                    ["response"] = Truncate(body, 2048)
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentik health check failed.");
            return HealthCheckResult.Unhealthy("Exception while reaching Authentik.", ex);
        }
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}


