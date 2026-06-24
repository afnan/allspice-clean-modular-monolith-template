using System.Security.Claims;
using AllSpice.CleanModularMonolith.Identity.Abstractions.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AllSpice.CleanModularMonolith.ApiGateway.UnitTests.Authentication;

/// <summary>
/// C4: Keycloak realm roles live in the nested <c>realm_access.roles</c> claim. The portal bearer scheme's
/// <c>OnTokenValidated</c> must flatten them into standard <see cref="ClaimTypes.Role"/> claims so
/// <c>[Authorize(Roles = …)]</c> and <c>User.IsInRole(...)</c> work.
/// </summary>
public class RealmRoleMappingTests
{
    private static JwtBearerOptions ResolveErpOptions()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthentication()
            .AddIdentityPortals(o =>
            {
                o.ErpAuthority = "https://localhost/realms/test";
                o.ErpAudience = "erp-client";
            });

        return services.BuildServiceProvider()
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(IdentityPortalSchemes.Erp);
    }

    private static async Task<ClaimsPrincipal> RunTokenValidated(string realmAccessJson)
    {
        var options = ResolveErpOptions();
        var identity = new ClaimsIdentity(new[] { new Claim("realm_access", realmAccessJson) }, authenticationType: "test");
        var scheme = new AuthenticationScheme(IdentityPortalSchemes.Erp, IdentityPortalSchemes.Erp, typeof(JwtBearerHandler));
        var context = new TokenValidatedContext(new DefaultHttpContext(), scheme, options)
        {
            Principal = new ClaimsPrincipal(identity)
        };

        await options.Events!.OnTokenValidated!(context);
        return context.Principal!;
    }

    [Fact]
    public async Task Flattens_realm_roles_into_role_claims()
    {
        var principal = await RunTokenValidated("{\"roles\":[\"admin\",\"user\"]}");

        Assert.True(principal.IsInRole("admin"));
        Assert.True(principal.IsInRole("user"));
    }

    [Fact]
    public async Task Ignores_malformed_realm_access()
    {
        var principal = await RunTokenValidated("this is not json");

        Assert.Empty(principal.FindAll(ClaimTypes.Role));
    }
}
