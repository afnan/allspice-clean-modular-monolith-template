namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Options;

public sealed class AuthentikOptions
{
    /// <summary>
    /// Base URL of the Authentik instance (e.g. https://auth.example.com).
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Personal access token or API token with rights to query users and send invitations.
    /// </summary>
    public string ApiToken { get; set; } = string.Empty;

    /// <summary>
    /// Template used to look up users. Defaults to /api/v3/core/users/{0}/.
    /// </summary>
    public string UserLookupTemplate { get; set; } = "/api/v3/core/users/{0}/";

    /// <summary>
    /// Endpoint used to invite new users. Optional.
    /// </summary>
    public string? InvitationEndpoint { get; set; }

    /// <summary>
    /// When true, TLS certificate validation is skipped. Use only for local development.
    /// </summary>
    public bool AllowUntrustedCertificates { get; set; }
}


