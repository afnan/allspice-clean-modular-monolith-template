using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AllSpice.CleanModularMonolith.Identity.Application.Contracts.External;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Options;
using Ardalis.GuardClauses;
using Microsoft.Extensions.Options;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Services;

/// <summary>
/// Concrete <see cref="IExternalDirectoryClient"/> implementation backed by Keycloak's Admin REST API.
/// </summary>
public sealed class KeycloakDirectoryClient : IExternalDirectoryClient
{
    private readonly HttpClient _httpClient;
    private readonly KeycloakOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="KeycloakDirectoryClient"/> class.
    /// </summary>
    /// <param name="httpClient">Configured HTTP client targeting Keycloak Admin API.</param>
    /// <param name="options">Keycloak options governing lookups and invitations.</param>
    public KeycloakDirectoryClient(HttpClient httpClient, IOptions<KeycloakOptions> options)
    {
        Guard.Against.Null(httpClient);
        Guard.Against.Null(options);

        _httpClient = httpClient;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<bool> UserExistsAsync(string userId, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrWhiteSpace(userId);

        var response = await _httpClient.GetAsync(BuildUserLookupUrl(userId), cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    /// <inheritdoc />
    public async Task<string?> GetUserDisplayNameAsync(string userId, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrWhiteSpace(userId);

        var response = await _httpClient.GetAsync(BuildUserLookupUrl(userId), cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var document = await JsonDocument.ParseAsync(contentStream, cancellationToken: cancellationToken);
        var root = document.RootElement;

        // Keycloak user object has firstName and lastName, or username
        if (root.TryGetProperty("firstName", out var firstNameElement) && root.TryGetProperty("lastName", out var lastNameElement))
        {
            var firstName = firstNameElement.GetString();
            var lastName = lastNameElement.GetString();
            if (!string.IsNullOrWhiteSpace(firstName) || !string.IsNullOrWhiteSpace(lastName))
            {
                return $"{firstName} {lastName}".Trim();
            }
        }

        if (root.TryGetProperty("username", out var usernameElement))
        {
            return usernameElement.GetString();
        }

        return userId;
    }

    /// <inheritdoc />
    public async Task InviteUserAsync(string email, string displayName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.InvitationEndpoint))
        {
            return;
        }

        Guard.Against.NullOrWhiteSpace(email);
        Guard.Against.NullOrWhiteSpace(displayName);

        // Keycloak Admin API user creation format
        var nameParts = displayName.Split(' ', 2);
        var payload = new
        {
            username = email,
            email = email,
            firstName = nameParts.Length > 0 ? nameParts[0] : string.Empty,
            lastName = nameParts.Length > 1 ? nameParts[1] : string.Empty,
            enabled = true,
            emailVerified = false
        };

        var response = await _httpClient.PostAsJsonAsync(_options.InvitationEndpoint, payload, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Builds the user lookup endpoint path based on current configuration.
    /// </summary>
    /// <param name="userId">User identifier.</param>
    /// <returns>Resolved endpoint URL.</returns>
    private string BuildUserLookupUrl(string userId)
    {
        var template = _options.UserLookupTemplate;
        if (!string.IsNullOrWhiteSpace(template))
        {
            // Replace {realm} and {0} placeholders
            template = template.Replace("{realm}", _options.Realm, StringComparison.OrdinalIgnoreCase);
            template = template.Replace("{0}", Uri.EscapeDataString(userId), StringComparison.OrdinalIgnoreCase);
            return template;
        }

        // Default Keycloak Admin API path
        return $"/admin/realms/{_options.Realm}/users/{Uri.EscapeDataString(userId)}";
    }
}

