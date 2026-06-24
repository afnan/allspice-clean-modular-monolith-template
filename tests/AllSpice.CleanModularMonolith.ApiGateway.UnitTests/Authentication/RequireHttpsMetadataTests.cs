using AllSpice.CleanModularMonolith.Identity.Abstractions.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AllSpice.CleanModularMonolith.ApiGateway.UnitTests.Authentication;

/// <summary>
/// F-https regression: <c>RequireHttpsMetadata</c> defaults to <c>true</c>, which blocks local dev where
/// Keycloak is served over plain HTTP. <c>AddIdentityPortals</c> must let the host relax it (per environment)
/// and thread the choice onto every portal bearer scheme.
/// </summary>
public class RequireHttpsMetadataTests
{
    private static JwtBearerOptions Resolve(string scheme, Action<IdentityPortalOptions> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthentication().AddIdentityPortals(configure);

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(scheme);
    }

    [Fact]
    public void Defaults_to_requiring_https_metadata()
    {
        var options = Resolve(IdentityPortalSchemes.Erp, o =>
        {
            o.ErpAuthority = "https://localhost/realms/test";
            o.ErpAudience = "erp-client";
        });

        Assert.True(options.RequireHttpsMetadata);
    }

    [Fact]
    public void Honours_disabled_https_metadata_on_erp_scheme()
    {
        var options = Resolve(IdentityPortalSchemes.Erp, o =>
        {
            o.ErpAuthority = "http://localhost:8080/realms/test";
            o.ErpAudience = "erp-client";
            o.RequireHttpsMetadata = false;
        });

        Assert.False(options.RequireHttpsMetadata);
    }

    [Fact]
    public void Honours_disabled_https_metadata_on_public_scheme()
    {
        var options = Resolve(IdentityPortalSchemes.Public, o =>
        {
            o.ErpAuthority = "http://localhost:8080/realms/test";
            o.ErpAudience = "erp-client";
            o.PublicAuthority = "http://localhost:8080/realms/test";
            o.PublicAudience = "web-client";
            o.RequireHttpsMetadata = false;
        });

        Assert.False(options.RequireHttpsMetadata);
    }
}
