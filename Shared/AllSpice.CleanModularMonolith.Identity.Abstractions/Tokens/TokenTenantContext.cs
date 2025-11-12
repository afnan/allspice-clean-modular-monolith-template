using System.Security.Claims;
using AllSpice.CleanModularMonolith.Identity.Abstractions.Authentication;
using AllSpice.CleanModularMonolith.Identity.Abstractions.Claims;

namespace AllSpice.CleanModularMonolith.Identity.Abstractions.Tokens;

/// <summary>
/// Represents issuer/audience/portal information extracted from a JWT.
/// </summary>
/// <param name="Issuer">Token issuer value.</param>
/// <param name="Audience">Token audience value.</param>
/// <param name="Portal">Authentik portal identifier.</param>
public sealed record TokenTenantContext(string? Issuer, string? Audience, string? Portal)
{
    /// <summary>
    /// Creates a <see cref="TokenTenantContext"/> from the supplied claims principal.
    /// </summary>
    /// <param name="principal">The authenticated principal.</param>
    /// <param name="options">Optional portal options used to resolve the portal claim type.</param>
    /// <returns>A populated tenant context.</returns>
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

    /// <summary>
    /// Determines whether the context belongs to the specified portal value.
    /// </summary>
    /// <param name="portalValue">Portal identifier to compare (e.g. <c>erp</c>).</param>
    /// <returns><c>true</c> when the stored portal matches; otherwise <c>false</c>.</returns>
    public bool IsPortal(string portalValue) =>
        !string.IsNullOrWhiteSpace(Portal) &&
        Portal.Equals(portalValue, StringComparison.OrdinalIgnoreCase);
}

