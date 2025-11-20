namespace AllSpice.CleanModularMonolith.Identity.Abstractions.Authentication;

/// <summary>
/// Well-known authentication scheme names used by the modular portal configuration.
/// </summary>
public static class IdentityPortalSchemes
{
    /// <summary>Authentication scheme name for ERP (internal) portal tokens.</summary>
    public const string Erp = "AuthentikErp";

    /// <summary>Authentication scheme name for public portal tokens.</summary>
    public const string Public = "AuthentikPublic";
}
