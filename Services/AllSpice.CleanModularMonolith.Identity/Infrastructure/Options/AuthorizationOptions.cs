namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Options;

public sealed class AuthorizationOptions
{
    public const string SectionName = "Authorization";

    /// <summary>Realm-role name auto-granted authz.read + authz.manage on startup. Null = no bootstrap.</summary>
    public string? BootstrapAdminRole { get; set; }
}
