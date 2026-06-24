using System.Security.Claims;
using AllSpice.CleanModularMonolith.ApiGateway.Identity;
using AllSpice.CleanModularMonolith.SharedKernel.Identity;
using Microsoft.AspNetCore.Http;

namespace AllSpice.CleanModularMonolith.ApiGateway.UnitTests.Identity;

/// <summary>
/// F-identity regression: audit columns must hold the canonical local UUID, not the Keycloak external
/// subject. The gateway resolves <c>sub → local Guid</c> once per request and caches it in
/// <see cref="ICurrentUserContext"/>; the audit interceptor then stamps the local id.
/// </summary>
public class CurrentUserResolutionMiddlewareTests
{
    private sealed class FakeResolver : IUserExternalIdResolver
    {
        private readonly Dictionary<string, Guid> _externalToLocal;

        public FakeResolver(Dictionary<string, Guid> externalToLocal) => _externalToLocal = externalToLocal;

        public int CallCount { get; private set; }

        public Task<string?> GetExternalIdByLocalIdAsync(Guid localUserId, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);

        public Task<Guid?> GetLocalIdByExternalIdAsync(string externalUserId, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(_externalToLocal.TryGetValue(externalUserId, out var local) ? local : (Guid?)null);
        }
    }

    private static DefaultHttpContext AuthenticatedContext(string subject)
    {
        var identity = new ClaimsIdentity(new[] { new Claim("sub", subject) }, authenticationType: "test");
        return new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
    }

    private static DefaultHttpContext AnonymousContext()
        => new() { User = new ClaimsPrincipal(new ClaimsIdentity()) };

    [Fact]
    public async Task Resolves_and_caches_local_id_for_authenticated_user()
    {
        var local = Guid.NewGuid();
        var resolver = new FakeResolver(new() { ["ext-1"] = local });
        var userContext = new CurrentUserContext();
        var nextCalled = false;
        var middleware = new CurrentUserResolutionMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(AuthenticatedContext("ext-1"), userContext, resolver);

        Assert.Equal(local, userContext.LocalUserId);
        Assert.Equal(1, resolver.CallCount);
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task Does_not_resolve_for_anonymous_request()
    {
        var resolver = new FakeResolver(new());
        var userContext = new CurrentUserContext();
        var middleware = new CurrentUserResolutionMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(AnonymousContext(), userContext, resolver);

        Assert.Null(userContext.LocalUserId);
        Assert.Equal(0, resolver.CallCount);
    }

    [Fact]
    public async Task Leaves_context_unresolved_when_user_not_synced_locally()
    {
        var resolver = new FakeResolver(new()); // "ext-unknown" maps to nothing
        var userContext = new CurrentUserContext();
        var middleware = new CurrentUserResolutionMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(AuthenticatedContext("ext-unknown"), userContext, resolver);

        Assert.Null(userContext.LocalUserId);
    }
}
