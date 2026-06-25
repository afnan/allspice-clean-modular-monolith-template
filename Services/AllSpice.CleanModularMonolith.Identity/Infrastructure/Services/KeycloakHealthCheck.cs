using System.Net.Http.Headers;
using System.Text.Json;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Extensions;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Options;
using AllSpice.CleanModularMonolith.SharedKernel.Common;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Services;

/// <summary>
/// Health check that verifies connectivity to the backing Keycloak instance.
/// </summary>
public sealed class KeycloakHealthCheck(
    IHttpClientFactory httpClientFactory,
    IOptions<KeycloakOptions> options,
    ILogger<KeycloakHealthCheck> logger) : IHealthCheck
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IOptions<KeycloakOptions> _options = options;
    private readonly ILogger<KeycloakHealthCheck> _logger = logger;

    private static readonly Uri HealthEndpoint = new("/users?max=1", UriKind.Relative);

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Value.IsAdminConfigured)
        {
            // The IdP hasn't been linked yet — report Degraded (not Unhealthy) so a freshly-scaffolded app is
            // "up and running" rather than failing readiness. Becomes a real connectivity check once configured.
            return HealthCheckResult.Degraded(
                "Keycloak is not linked yet. Set Identity:Keycloak (ServiceName or BaseUrl, plus an ApiToken or " +
                "ClientId/ClientSecret) to enable directory/auth features.");
        }

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
            // Keycloak error bodies can echo bearer tokens or partial session data.
            // Truncate aggressively before logging so we don't leak credentials into
            // the log sink.
            var redactedBody = body.Truncate(256);
            _logger.LogWarning(
                "Keycloak health request failed with status {Status}. Response (truncated): {ResponseBody}",
                response.StatusCode,
                redactedBody);

            return HealthCheckResult.Unhealthy(
                $"Keycloak responded with status {(int)response.StatusCode}",
                data: new Dictionary<string, object>
                {
                    ["statusCode"] = (int)response.StatusCode,
                    ["response"] = redactedBody
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Keycloak health check failed.");
            return HealthCheckResult.Unhealthy("Exception while reaching Keycloak.", ex);
        }
    }

}

