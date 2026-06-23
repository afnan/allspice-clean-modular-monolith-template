using System.Net.Http.Json;
using System.Text.Json;
using AllSpice.CleanModularMonolith.SharedKernel.Exceptions;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Services;

/// <summary>
/// Provides cached access tokens for Keycloak Admin API via client credentials flow.
/// Falls back to a static ApiToken when ClientId/ClientSecret are not configured.
/// Includes explicit timeout handling and custom exception types for production robustness.
/// </summary>
public sealed class KeycloakTokenProvider : IDisposable
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

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

        if (string.IsNullOrWhiteSpace(opts.ClientId) || string.IsNullOrWhiteSpace(opts.ClientSecret))
        {
            return opts.ApiToken;
        }

        if (_cachedToken is not null && DateTimeOffset.UtcNow.AddMinutes(5) < _tokenExpiry)
        {
            return _cachedToken;
        }

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
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
        // Match the scheme used by the Keycloak admin HttpClient
        // (IdentityModuleExtensions.ConfigureKeycloakClient). Aspire service discovery
        // resolves bare http://service-name; the previous "http+https://" scheme
        // requires service-discovery middleware on the HttpClient and is not consistent
        // with how the rest of the module addresses Keycloak.
        var baseUrl = !string.IsNullOrWhiteSpace(opts.ServiceName)
            ? $"http://{opts.ServiceName}"
            : opts.BaseUrl.TrimEnd('/');

        var tokenUrl = $"{baseUrl}/realms/{opts.Realm}/protocol/openid-connect/token";

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(RequestTimeout);

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = RequestTimeout;

            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = opts.ClientId,
                ["client_secret"] = opts.ClientSecret
            });

            var response = await client.PostAsync(tokenUrl, content, timeoutCts.Token);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(timeoutCts.Token);
            var doc = await JsonDocument.ParseAsync(stream, cancellationToken: timeoutCts.Token);

            var accessToken = doc.RootElement.GetProperty("access_token").GetString()
                ?? throw new InvalidOperationException("Keycloak token response missing access_token.");

            var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var expProp) && expProp.TryGetInt32(out var exp) && exp > 0
                ? TimeSpan.FromSeconds(exp)
                : TimeSpan.FromMinutes(5);

            _cachedToken = accessToken;
            _tokenExpiry = DateTimeOffset.UtcNow.Add(expiresIn);

            _logger.LogDebug("Keycloak access token refreshed, expires in {ExpiresIn}s", expiresIn.TotalSeconds);

            return accessToken;
        }
        catch (OperationCanceledException ex) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Timeout requesting access token from Keycloak at {TokenUrl}", tokenUrl);
            throw new IdentityServerUnreachableException(
                $"Keycloak token request timed out after {RequestTimeout.TotalSeconds}s at {tokenUrl}.", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error requesting access token from Keycloak at {TokenUrl}", tokenUrl);
            throw new IdentityServerUnreachableException(
                $"Failed to reach Keycloak at {tokenUrl}: {ex.Message}", ex);
        }
    }

    public void Dispose()
    {
        _semaphore.Dispose();
    }
}
