using System.Security.Claims;
using AllSpice.CleanModularMonolith.SharedKernel.Identity;

namespace AllSpice.CleanModularMonolith.ApiGateway.Identity;

/// <summary>
/// Resolves the authenticated request's external IdP subject to the canonical local user UUID exactly
/// once per request and caches it in <see cref="ICurrentUserContext"/>. Downstream audit stamping then
/// records the local id (per the local-UUID-is-canonical convention) instead of the Keycloak subject.
/// Runs after authentication so <see cref="HttpContext.User"/> is populated; anonymous requests are a
/// no-op. Costs one directory lookup per authenticated request (the user is fixed for the request).
/// </summary>
public sealed class CurrentUserResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public CurrentUserResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ICurrentUserContext currentUserContext, IUserExternalIdResolver resolver)
    {
        var user = context.User;
        if (user.Identity?.IsAuthenticated == true)
        {
            var externalId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
            if (!string.IsNullOrWhiteSpace(externalId))
            {
                var localUserId = await resolver.GetLocalIdByExternalIdAsync(externalId, context.RequestAborted);
                currentUserContext.Resolve(localUserId);
            }
        }

        await _next(context);
    }
}
