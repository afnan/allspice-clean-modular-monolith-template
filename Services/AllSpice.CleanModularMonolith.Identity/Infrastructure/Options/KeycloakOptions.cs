namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Options;

public sealed class KeycloakOptions
{
    /// <summary>
    /// Service name for Aspire service discovery (e.g. "keycloak"). 
    /// When provided, takes precedence over BaseUrl and uses service discovery.
    /// </summary>
    public string? ServiceName { get; set; }

    /// <summary>
    /// Base URL of the Keycloak instance (e.g. http://localhost:8080).
    /// Used as fallback when ServiceName is not provided.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Keycloak realm name (e.g. allspice).
    /// </summary>
    public string Realm { get; set; } = string.Empty;

    /// <summary>
    /// Admin client secret or service account token with rights to query users and send invitations.
    /// </summary>
    public string ApiToken { get; set; } = string.Empty;

    /// <summary>
    /// Template used to look up users. Defaults to /admin/realms/{realm}/users/{0}.
    /// </summary>
    public string UserLookupTemplate { get; set; } = "/admin/realms/{realm}/users/{0}";

    /// <summary>
    /// Endpoint used to invite new users. Optional.
    /// </summary>
    public string? InvitationEndpoint { get; set; }

    /// <summary>
    /// When true, TLS certificate validation is skipped. Use only for local development.
    /// </summary>
    public bool AllowUntrustedCertificates { get; set; }
}

