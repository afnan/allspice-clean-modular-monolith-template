using System.Security.Claims;
using AllSpice.CleanModularMonolith.ApiGateway.Identity;
using AllSpice.CleanModularMonolith.SharedKernel.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AllSpice.CleanModularMonolith.ApiGateway.UnitTests.Identity;

/// <summary>
/// F-identity regression: the provider must surface the canonical local UUID (resolved into
/// <see cref="ICurrentUserContext"/> for the request), never the raw Keycloak subject, and must
/// be unattributed (<c>null</c>) when there is no authenticated user or no local mapping.
/// </summary>
public class HttpContextCurrentUserProviderTests
{
    private static HttpContext Context(bool authenticated, Guid? resolvedLocalId)
    {
        var identity = authenticated
            ? new ClaimsIdentity(new[] { new Claim("sub", "ext-1") }, authenticationType: "test")
            : new ClaimsIdentity();

        var userContext = new CurrentUserContext();
        if (resolvedLocalId is not null)
        {
            userContext.Resolve(resolvedLocalId.Value);
        }

        var services = new ServiceCollection();
        services.AddSingleton<ICurrentUserContext>(userContext);

        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity),
            RequestServices = services.BuildServiceProvider()
        };
    }

    private static HttpContextCurrentUserProvider ProviderFor(HttpContext? context)
        => new(new HttpContextAccessor { HttpContext = context });

    [Fact]
    public void Returns_resolved_local_id_for_authenticated_user()
    {
        var local = Guid.NewGuid();
        var provider = ProviderFor(Context(authenticated: true, resolvedLocalId: local));

        Assert.Equal(local.ToString(), provider.UserId);
    }

    [Fact]
    public void Returns_null_when_unauthenticated()
    {
        var provider = ProviderFor(Context(authenticated: false, resolvedLocalId: Guid.NewGuid()));

        Assert.Null(provider.UserId);
    }

    [Fact]
    public void Returns_null_when_local_id_unresolved()
    {
        var provider = ProviderFor(Context(authenticated: true, resolvedLocalId: null));

        Assert.Null(provider.UserId);
    }

    [Fact]
    public void Returns_null_outside_an_http_request()
    {
        var provider = ProviderFor(context: null);

        Assert.Null(provider.UserId);
    }
}
