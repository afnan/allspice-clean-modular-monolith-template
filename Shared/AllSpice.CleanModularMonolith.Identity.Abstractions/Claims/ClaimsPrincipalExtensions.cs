using System.Security.Claims;
using Ardalis.GuardClauses;

namespace AllSpice.CleanModularMonolith.Identity.Abstractions.Claims;

/// <summary>
/// Provides helper methods for working with identity claims produced by Keycloak.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Resolves the external identity-provider subject for the principal, trying the common claim names in
    /// order: <see cref="ClaimTypes.NameIdentifier"/>, then <c>sub</c>, <c>oid</c>, and <c>client_id</c>.
    /// Returns <c>null</c> when none are present. This is the external (Keycloak) id — appropriate at JWT /
    /// SignalR boundaries; resolve it to the canonical local UUID before persisting or audit-stamping.
    /// </summary>
    public static string? GetSubjectId(this ClaimsPrincipal principal)
    {
        Guard.Against.Null(principal);
        return principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? principal.FindFirst("sub")?.Value
            ?? principal.FindFirst("oid")?.Value
            ?? principal.FindFirst("client_id")?.Value;
    }

    /// <summary>
    /// Returns the token issuer (typically the Keycloak application URL).
    /// </summary>
    public static string? GetIssuer(this ClaimsPrincipal principal)
    {
        Guard.Against.Null(principal);
        return principal.FindFirstValue(IdentityClaimTypes.Issuer);
    }

    /// <summary>
    /// Returns the token audience (client identifier).
    /// </summary>
    public static string? GetAudience(this ClaimsPrincipal principal)
    {
        Guard.Against.Null(principal);
        return principal.FindFirstValue(IdentityClaimTypes.Audience);
    }

    /// <summary>
    /// Returns the Keycloak portal identifier (e.g. <c>erp</c>, <c>public</c>) assigned to the principal.
    /// </summary>
    /// <param name="principal">The authenticated user.</param>
    /// <param name="claimTypeOverride">Optional claim type override when Keycloak configuration uses a different name.</param>
    public static string? GetPortal(this ClaimsPrincipal principal, string? claimTypeOverride = null)
    {
        Guard.Against.Null(principal);
        var claimType = string.IsNullOrWhiteSpace(claimTypeOverride)
            ? IdentityClaimTypes.Portal
            : claimTypeOverride;

        return principal.FindFirstValue(claimType);
    }
}
