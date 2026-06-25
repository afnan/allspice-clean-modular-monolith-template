using AllSpice.CleanModularMonolith.Identity.Infrastructure.Options;

namespace AllSpice.CleanModularMonolith.Identity.Application.UnitTests;

/// <summary>
/// The IdP is linked by the operator after scaffolding, so a fully-empty Keycloak section is a valid
/// "not linked yet" state — the app must boot. A partially-filled section, however, is almost certainly a
/// misconfiguration and must fail fast at startup.
/// </summary>
public class KeycloakOptionsValidatorTests
{
    private readonly KeycloakOptionsValidator _validator = new();

    [Fact]
    public void Empty_configuration_is_valid_so_the_app_boots_unlinked()
    {
        var result = _validator.Validate(name: null, new KeycloakOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Base_without_realm_fails_because_it_is_a_partial_configuration()
    {
        var options = new KeycloakOptions { ServiceName = "keycloak", Realm = string.Empty };

        var result = _validator.Validate(name: null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f => f.Contains("Realm", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Realm_without_a_base_fails_because_it_is_a_partial_configuration()
    {
        var options = new KeycloakOptions { Realm = "demo" };

        var result = _validator.Validate(name: null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, f =>
            f.Contains("ServiceName", StringComparison.OrdinalIgnoreCase)
            || f.Contains("BaseUrl", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void A_base_plus_realm_is_a_complete_configuration_and_is_valid()
    {
        var options = new KeycloakOptions { ServiceName = "keycloak", Realm = "demo" };

        var result = _validator.Validate(name: null, options);

        Assert.True(result.Succeeded);
    }
}
