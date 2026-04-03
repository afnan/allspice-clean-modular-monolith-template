using System.Net.Http.Json;
using System.Text.Json;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Services;

/// <summary>
/// Provides cached access tokens for Keycloak Admin API via client credentials flow.
/// Falls back to a static ApiToken when ClientId/ClientSecret are not configured.
/// </summary>
public sealed class KeycloakTokenProvider : IDisposable
{
    private readonly IOptions<KeycloakOptions> _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<KeycloakTokenProvider> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private string? _cachedToken;
    private DateTimeOffset _tokenExpiry = DateTimeOffset.MinValue;

    public KeycloakTokenProvider(
        IOptions<KeycloakOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<KeycloakTokenProvider> logger)
    {
        _options = options;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Gets a valid access token, refreshing if necessary.
    /// Returns the static ApiToken if client credentials are not configured.
    /// </summary>
    public async Task<string> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        var opts = _options.Value;

        // Fall back to raw API token if client credentials are not configured
        if (string.IsNullOrWhiteSpace(opts.ClientId) || string.IsNullOrWhiteSpace(opts.ClientSecret))
        {
            return opts.ApiToken;
        }

        // Return cached token if still valid (with 5-minute buffer)
        if (_cachedToken is not null && DateTimeOffset.UtcNow.AddMinutes(5) < _tokenExpiry)
        {
            return _cachedToken;
        }

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock
            if (_cachedToken is not null && DateTimeOffset.UtcNow.AddMinutes(5) < _tokenExpiry)
            {
                return _cachedToken;
            }

            return await RefreshTokenAsync(opts, cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<string> RefreshTokenAsync(KeycloakOptions opts, CancellationToken cancellationToken)
    {
        var baseUrl = !string.IsNullOrWhiteSpace(opts.ServiceName)
            ? $"http://{opts.ServiceName}"
            : opts.BaseUrl.TrimEnd('/');

        var tokenUrl = $"{baseUrl}/realms/{opts.Realm}/protocol/openid-connect/token";

        var client = _httpClientFactory.CreateClient();
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = opts.ClientId,
            ["client_secret"] = opts.ClientSecret
        });

        var response = await client.PostAsync(tokenUrl, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var accessToken = doc.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Keycloak token response missing access_token.");

        var expiresIn = doc.RootElement.GetProperty("expires_in").GetInt32();

        _cachedToken = accessToken;
        _tokenExpiry = DateTimeOffset.UtcNow.AddSeconds(expiresIn);

        _logger.LogDebug("Keycloak access token refreshed, expires in {ExpiresIn}s", expiresIn);

        return accessToken;
    }

    public void Dispose()
    {
        _semaphore.Dispose();
    }
}
