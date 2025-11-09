using System.Security.Claims;
using Ardalis.GuardClauses;

namespace AllSpice.CleanModularMonolith.Identity.Abstractions.Claims;

/// <summary>
/// Provides helper methods for working with identity claims produced by Authentik.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Determines whether the principal contains the supplied module/role assignment encoded in the <c>module_roles</c> claim.
    /// </summary>
    /// <param name="principal">The authenticated user.</param>
    /// <param name="moduleKey">The module identifier (e.g. <c>HR</c>).</param>
    /// <param name="roleKey">The role within the module (e.g. <c>Admin</c>).</param>
    /// <returns><c>true</c> when the claim set contains the module-role pair; otherwise <c>false</c>.</returns>
    public static bool HasModuleRole(this ClaimsPrincipal principal, string moduleKey, string roleKey)
    {
        Guard.Against.Null(principal);
        if (string.IsNullOrWhiteSpace(moduleKey) || string.IsNullOrWhiteSpace(roleKey))
        {
            return false;
        }

        var claim = principal.FindFirst(IdentityClaimTypes.ModuleRoles);
        if (claim is null)
        {
            return false;
        }

        var assignments = claim.Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var assignment in assignments)
        {
            var parts = assignment.Split('=', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
            {
                continue;
            }

            var module = parts[0];
            var roles = parts[1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (!module.Equals(moduleKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (roles.Any(role => role.Equals(roleKey, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns the token issuer (typically the Authentik application URL).
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
    /// Returns the Authentik portal identifier (e.g. <c>erp</c>, <c>public</c>) assigned to the principal.
    /// </summary>
    /// <param name="principal">The authenticated user.</param>
    /// <param name="claimTypeOverride">Optional claim type override when Authentik configuration uses a different name.</param>
    public static string? GetPortal(this ClaimsPrincipal principal, string? claimTypeOverride = null)
    {
        Guard.Against.Null(principal);
        var claimType = string.IsNullOrWhiteSpace(claimTypeOverride)
            ? IdentityClaimTypes.Portal
            : claimTypeOverride;

        return principal.FindFirstValue(claimType);
    }
}
