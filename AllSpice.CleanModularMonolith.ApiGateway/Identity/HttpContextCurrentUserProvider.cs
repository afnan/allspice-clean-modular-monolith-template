using System.Security.Claims;
using AllSpice.CleanModularMonolith.SharedKernel.Identity;

namespace AllSpice.CleanModularMonolith.ApiGateway.Identity;

/// <summary>
/// <see cref="ICurrentUserProvider"/> backed by the authenticated <see cref="ClaimsPrincipal"/> on the
/// current request. Registered as a singleton; <see cref="IHttpContextAccessor"/> supplies the per-request
/// context at call time, so it is safe for the (singleton, pooled-DbContext) audit interceptor to consume.
/// Returns <c>null</c> outside an HTTP request (e.g. background jobs), which yields unattributed audit stamps.
/// </summary>
public sealed class HttpContextCurrentUserProvider : ICurrentUserProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextCurrentUserProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? UserId
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            return user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        }
    }
}
