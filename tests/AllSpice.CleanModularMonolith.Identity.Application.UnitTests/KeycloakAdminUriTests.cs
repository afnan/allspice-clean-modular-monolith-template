using AllSpice.CleanModularMonolith.Identity.Infrastructure.Options;

namespace AllSpice.CleanModularMonolith.Identity.Application.UnitTests;

/// <summary>
/// The admin HttpClient is resolved on every request (CurrentUserResolutionMiddleware), so it must be
/// CONSTRUCTABLE before the IdP is linked. <see cref="KeycloakOptions.AdminApiBaseAddress"/> returns null
/// while unlinked (client built without a base address; no call is made) and the realm-scoped admin URL
/// once configured.
/// </summary>
public class KeycloakAdminUriTests
{
    [Fact]
    public void Returns_null_when_nothing_is_configured()
    {
        Assert.Null(new KeycloakOptions().AdminApiBaseAddress);
    }

    [Fact]
    public void Returns_null_when_realm_is_missing()
    {
        var options = new KeycloakOptions { ServiceName = "keycloak", Realm = string.Empty };

        Assert.Null(options.AdminApiBaseAddress);
    }

    [Fact]
    public void Returns_null_when_a_base_is_missing()
    {
        var options = new KeycloakOptions { Realm = "demo" };

        Assert.Null(options.AdminApiBaseAddress);
    }

    [Fact]
    public void Uses_service_discovery_when_a_service_name_is_provided()
    {
        var options = new KeycloakOptions { ServiceName = "keycloak", BaseUrl = "http://ignored:9999", Realm = "demo" };

        Assert.Equal(new Uri("http://keycloak/admin/realms/demo/"), options.AdminApiBaseAddress);
    }

    [Fact]
    public void Falls_back_to_base_url_and_trims_a_trailing_slash()
    {
        var options = new KeycloakOptions { BaseUrl = "http://localhost:8080/", Realm = "demo" };

        Assert.Equal(new Uri("http://localhost:8080/admin/realms/demo/"), options.AdminApiBaseAddress);
    }

    [Fact]
    public void Base_address_has_a_trailing_slash_so_relative_admin_paths_stay_under_the_realm()
    {
        // Regression guard: the admin clients issue relative URIs WITHOUT a leading slash (e.g. "users?...",
        // "roles"). Without the trailing slash on the base, RFC 3986 resolution would drop the realm segment
        // ("http://keycloak/admin/realms/users?...") and every admin call (user sync, role sync, ...) would
        // 404 once Keycloak is linked.
        var options = new KeycloakOptions { ServiceName = "keycloak", Realm = "demo" };
        var baseAddress = options.AdminApiBaseAddress!;

        Assert.EndsWith("/", baseAddress.AbsoluteUri);

        // A no-leading-slash relative path resolves UNDER the realm.
        Assert.Equal(
            new Uri("http://keycloak/admin/realms/demo/users?first=0&max=100"),
            new Uri(baseAddress, "users?first=0&max=100"));

        // A leading-slash absolute path (as produced by UserLookupTemplate) still resolves correctly.
        Assert.Equal(
            new Uri("http://keycloak/admin/realms/demo/users/abc"),
            new Uri(baseAddress, "/admin/realms/demo/users/abc"));
    }
}
