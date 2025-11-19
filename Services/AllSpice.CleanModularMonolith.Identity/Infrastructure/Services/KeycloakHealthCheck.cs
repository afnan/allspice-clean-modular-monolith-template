using System.Net.Http.Headers;
using System.Text.Json;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Services;

/// <summary>
/// Health check that verifies connectivity to the backing Keycloak instance.
/// </summary>
public sealed class KeycloakHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<KeycloakHealthCheck> _logger;

    private static readonly Uri HealthEndpoint = new("/users?max=1", UriKind.Relative);

    /// <summary>
    /// Initializes a new instance of the <see cref="KeycloakHealthCheck"/> class.
    /// </summary>
    public KeycloakHealthCheck(
        IHttpClientFactory httpClientFactory,
        ILogger<KeycloakHealthCheck> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient(IdentityModuleExtensions.KeycloakHttpClientName);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, HealthEndpoint);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await client.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy("Authenticated request to Keycloak succeeded.");
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "Keycloak health request failed with status {Status}. Response: {ResponseBody}",
                response.StatusCode,
                body);

            return HealthCheckResult.Unhealthy(
                $"Keycloak responded with status {(int)response.StatusCode}",
                data: new Dictionary<string, object>
                {
                    ["statusCode"] = (int)response.StatusCode,
                    ["response"] = Truncate(body, 2048)
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Keycloak health check failed.");
            return HealthCheckResult.Unhealthy("Exception while reaching Keycloak.", ex);
        }
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}

