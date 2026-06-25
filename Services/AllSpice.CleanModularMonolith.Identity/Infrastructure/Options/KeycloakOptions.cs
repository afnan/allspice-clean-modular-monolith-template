using System.ComponentModel.DataAnnotations;

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
    /// Keycloak realm name (e.g. allspice). Required only once the IdP is being configured — enforced
    /// conditionally by <see cref="KeycloakOptionsValidator"/> so an unlinked app can still boot.
    /// </summary>
    public string Realm { get; set; } = string.Empty;

    /// <summary>
    /// Admin client secret or service account token with rights to query users and send invitations.
    /// Falls back to client credentials flow when ClientId and ClientSecret are provided.
    /// </summary>
    public string ApiToken { get; set; } = string.Empty;

    /// <summary>
    /// OAuth 2.0 client ID for client credentials flow. Used by KeycloakTokenProvider.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// OAuth 2.0 client secret for client credentials flow. Used by KeycloakTokenProvider.
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Template used to look up users. Defaults to /admin/realms/{realm}/users/{0}.
    /// </summary>
    public string UserLookupTemplate { get; set; } = "/admin/realms/{realm}/users/{0}";

    /// <summary>
    /// When true, TLS certificate validation is skipped. Use only for local development.
    /// </summary>
    public bool AllowUntrustedCertificates { get; set; }

    /// <summary>
    /// HttpClient timeout for Keycloak admin API calls in seconds. Default 30.
    /// Values ≤ 0 are treated as the default. Long enough for slow realm operations,
    /// short enough that a hung Keycloak doesn't freeze invitation flows.
    /// </summary>
    [Range(0, 600)]
    public int RequestTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Whether Keycloak admin access is actually configured: a base (<see cref="ServiceName"/> or
    /// <see cref="BaseUrl"/>) PLUS admin credentials (an <see cref="ApiToken"/>, or a
    /// <see cref="ClientId"/>+<see cref="ClientSecret"/> for the client-credentials flow). When this is
    /// <c>false</c> the IdP simply hasn't been linked yet — admin-dependent features (Keycloak connectivity,
    /// user sync) report <c>Degraded</c> rather than <c>Unhealthy</c>, so a freshly-scaffolded app is "up and
    /// running" and becomes fully healthy once these settings are supplied.
    /// </summary>
    public bool IsAdminConfigured =>
        (!string.IsNullOrWhiteSpace(ServiceName) || !string.IsNullOrWhiteSpace(BaseUrl))
        && (!string.IsNullOrWhiteSpace(ApiToken)
            || (!string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret)));

    /// <summary>
    /// The Keycloak Admin REST API base address — derived from <see cref="ServiceName"/> (Aspire service
    /// discovery, preferred) or <see cref="BaseUrl"/>, scoped to <see cref="Realm"/>. Returns <c>null</c>
    /// while the IdP isn't linked (no realm, or no base) so the admin <c>HttpClient</c> can still be
    /// CONSTRUCTED without a base address — it is resolved on every request by the gateway's
    /// CurrentUserResolutionMiddleware, including anonymous <c>/health</c>. No call is made while unlinked
    /// (the Keycloak-dependent health checks short-circuit on <see cref="IsAdminConfigured"/> and the sync
    /// job is idle), so the missing base address is harmless until the IdP is configured.
    /// </summary>
    public Uri? AdminApiBaseAddress
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Realm))
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(ServiceName))
            {
                return new Uri($"http://{ServiceName}/admin/realms/{Realm}", UriKind.Absolute);
            }

            return string.IsNullOrWhiteSpace(BaseUrl)
                ? null
                : new Uri($"{BaseUrl.TrimEnd('/')}/admin/realms/{Realm}", UriKind.Absolute);
        }
    }
}

