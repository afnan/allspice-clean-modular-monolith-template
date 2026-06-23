using AllSpice.CleanModularMonolith.Identity.Abstractions.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AllSpice.CleanModularMonolith.ApiGateway.UnitTests.Authentication;

/// <summary>
/// F5 regression: SignalR sends the JWT as the <c>access_token</c> query parameter (browsers can't set the
/// Authorization header on WebSocket/SSE). The ERP bearer scheme must read it for <c>/hubs</c> paths, and
/// must NOT read it for ordinary API paths.
/// </summary>
public class HubBearerTokenTests
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

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(IdentityPortalSchemes.Erp);
    }

    private static MessageReceivedContext MakeContext(JwtBearerOptions options, string path, string accessToken)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = path;
        httpContext.Request.QueryString = new QueryString($"?access_token={accessToken}");

        var scheme = new AuthenticationScheme(IdentityPortalSchemes.Erp, IdentityPortalSchemes.Erp, typeof(JwtBearerHandler));
        return new MessageReceivedContext(httpContext, scheme, options);
    }

    [Fact]
    public async Task Hub_path_reads_access_token_from_query()
    {
        var options = ResolveErpOptions();
        var context = MakeContext(options, "/hubs/app", "abc123");

        await options.Events!.OnMessageReceived!(context);

        Assert.Equal("abc123", context.Token);
    }

    [Fact]
    public async Task Non_hub_path_ignores_query_access_token()
    {
        var options = ResolveErpOptions();
        var context = MakeContext(options, "/api/users", "abc123");

        await options.Events!.OnMessageReceived!(context);

        Assert.Null(context.Token);
    }
}
