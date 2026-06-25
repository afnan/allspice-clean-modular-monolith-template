using AllSpice.CleanModularMonolith.Identity.Infrastructure.Options;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AllSpice.CleanModularMonolith.Identity.Application.UnitTests;

/// <summary>
/// "App up and running" before the IdP is linked: when Keycloak admin access isn't configured, the
/// Keycloak-dependent health checks must report Degraded (so /health stays 200), not Unhealthy (503).
/// </summary>
public class KeycloakHealthDegradationTests
{
    [Theory]
    [InlineData("keycloak", "", "token", "", "", true)]          // service name + api token
    [InlineData("", "http://kc:8080", "token", "", "", true)]    // base url + api token
    [InlineData("keycloak", "", "", "client", "secret", true)]   // service name + client credentials
    [InlineData("keycloak", "", "", "", "", false)]              // base but no credentials
    [InlineData("", "", "token", "", "", false)]                 // credentials but no base
    [InlineData("keycloak", "", "", "client", "", false)]        // client id without secret
    [InlineData("", "", "", "", "", false)]                      // nothing configured
    public void IsAdminConfigured_requires_a_base_and_credentials(
        string serviceName, string baseUrl, string apiToken, string clientId, string clientSecret, bool expected)
    {
        var options = new KeycloakOptions
        {
            ServiceName = serviceName,
            BaseUrl = baseUrl,
            ApiToken = apiToken,
            ClientId = clientId,
            ClientSecret = clientSecret,
            Realm = "realm"
        };

        Assert.Equal(expected, options.IsAdminConfigured);
    }

    [Fact]
    public async Task KeycloakHealthCheck_is_degraded_and_makes_no_call_when_not_configured()
    {
        // Strict: the test fails if the check tries to create an HttpClient — it must short-circuit first.
        var factory = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        var options = Options.Create(new KeycloakOptions { Realm = "realm" }); // no base + no credentials

        var check = new KeycloakHealthCheck(factory.Object, options, NullLogger<KeycloakHealthCheck>.Instance);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
        factory.Verify(f => f.CreateClient(It.IsAny<string>()), Times.Never);
    }
}
