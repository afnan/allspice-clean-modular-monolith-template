namespace AllSpice.CleanModularMonolith.Identity.Abstractions.Claims;

/// <summary>
/// Strongly-typed claim type identifiers emitted by the identity provider.
/// </summary>
public static class IdentityClaimTypes
{
    /// <summary>JWT issuer claim.</summary>
    public const string Issuer = "iss";
    /// <summary>JWT audience claim.</summary>
    public const string Audience = "aud";
    /// <summary>OAuth scope claim.</summary>
    public const string Scope = "scope";
    /// <summary>Standard roles claim.</summary>
    public const string Roles = "roles";
    /// <summary>Custom claim storing module-role assignments.</summary>
    public const string ModuleRoles = "module_roles";
    /// <summary>Authentik portal identifier claim.</summary>
    public const string Portal = "ak_portal";
}
