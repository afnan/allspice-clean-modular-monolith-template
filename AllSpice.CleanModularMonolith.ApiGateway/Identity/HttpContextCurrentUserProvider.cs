using AllSpice.CleanModularMonolith.SharedKernel.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace AllSpice.CleanModularMonolith.ApiGateway.Identity;

/// <summary>
/// <see cref="ICurrentUserProvider"/> backed by the current request. Returns the <em>canonical local
/// user UUID</em> (resolved once per request into <see cref="ICurrentUserContext"/> by
/// <see cref="CurrentUserResolutionMiddleware"/>), never the external IdP subject — so audit columns hold
/// the local identity. Registered as a singleton; <see cref="IHttpContextAccessor"/> supplies the per-request
/// context (and request-scoped services) at call time, so it is safe for the (singleton, pooled-DbContext)
/// audit interceptor to consume. Returns <c>null</c> outside an HTTP request (e.g. background jobs), or when
/// the authenticated subject has not yet been mirrored locally — both yield unattributed audit stamps.
/// </summary>
public sealed class HttpContextCurrentUserProvider(IHttpContextAccessor httpContextAccessor) : ICurrentUserProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    public string? UserId
    {
        get
        {
            var context = _httpContextAccessor.HttpContext;
            if (context?.User.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            var localUserId = context.RequestServices.GetService<ICurrentUserContext>()?.LocalUserId;
            return localUserId?.ToString();
        }
    }
}
