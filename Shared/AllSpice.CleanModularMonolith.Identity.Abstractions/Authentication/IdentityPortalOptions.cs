using AllSpice.CleanModularMonolith.Identity.Abstractions.Claims;

namespace AllSpice.CleanModularMonolith.Identity.Abstractions.Authentication;

/// <summary>
/// Configuration for wiring multiple Authentik applications (e.g. ERP and public portals) into the authentication pipeline.
/// </summary>
public sealed class IdentityPortalOptions
{
    /// <summary>
    /// Name of the authentication scheme used for the ERP portal.
    /// </summary>
    public string ErpScheme { get; set; } = IdentityPortalSchemes.Erp;

    /// <summary>
    /// Name of the authentication scheme used for the public/consumer portal.
    /// </summary>
    public string PublicScheme { get; set; } = IdentityPortalSchemes.Public;

    /// <summary>
    /// Authority (issuer) for ERP tokens (e.g. https://auth.example.com/application/o/erp/).
    /// </summary>
    public string ErpAuthority { get; set; } = string.Empty;

    /// <summary>
    /// Audience / client ID for ERP tokens.
    /// </summary>
    public string ErpAudience { get; set; } = string.Empty;

    /// <summary>
    /// Authority for public tokens (e.g. https://auth.example.com/application/o/public/).
    /// </summary>
    public string PublicAuthority { get; set; } = string.Empty;

    /// <summary>
    /// Audience / client ID for public tokens.
    /// </summary>
    public string PublicAudience { get; set; } = string.Empty;

    /// <summary>
    /// Claim type emitted by Authentik that identifies which portal issued the token.
    /// </summary>
    public string PortalClaimType { get; set; } = IdentityClaimTypes.Portal;

    /// <summary>
    /// Claim value used to indicate ERP portal tokens.
    /// </summary>
    public string ErpPortalValue { get; set; } = "erp";

    /// <summary>
    /// Claim value used to indicate public portal tokens.
    /// </summary>
    public string PublicPortalValue { get; set; } = "public";

    /// <summary>
    /// When true, adds the public scheme as the default challenge (useful for public SPAs).
    /// </summary>
    public bool UsePublicAsDefaultChallenge { get; set; }
}


