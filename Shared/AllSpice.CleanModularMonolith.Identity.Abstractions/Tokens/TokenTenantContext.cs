using System.Security.Claims;
using AllSpice.CleanModularMonolith.Identity.Abstractions.Authentication;
using AllSpice.CleanModularMonolith.Identity.Abstractions.Claims;

namespace AllSpice.CleanModularMonolith.Identity.Abstractions.Tokens;

public sealed record TokenTenantContext(string? Issuer, string? Audience, string? Portal)
{
    public static TokenTenantContext FromPrincipal(ClaimsPrincipal principal, IdentityPortalOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var issuer = principal.GetIssuer();
        var audience = principal.GetAudience();

        string? portal = null;
        if (options is not null)
        {
            portal = principal.GetPortal(options.PortalClaimType);
        }
        else
        {
            portal = principal.GetPortal();
        }

        return new TokenTenantContext(issuer, audience, portal);
    }

    public bool IsPortal(string portalValue) =>
        !string.IsNullOrWhiteSpace(Portal) &&
        Portal.Equals(portalValue, StringComparison.OrdinalIgnoreCase);
}

