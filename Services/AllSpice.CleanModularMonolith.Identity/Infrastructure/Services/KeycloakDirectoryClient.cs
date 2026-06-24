using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AllSpice.CleanModularMonolith.Identity.Application.Contracts.External;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Options;
using Ardalis.GuardClauses;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Services;

/// <summary>
/// Concrete <see cref="IExternalDirectoryClient"/> implementation backed by Keycloak's Admin REST API.
/// </summary>
public sealed class KeycloakDirectoryClient(
    HttpClient httpClient,
    IOptions<KeycloakOptions> options,
    ILogger<KeycloakDirectoryClient> logger) : IExternalDirectoryClient
{
    private readonly HttpClient _httpClient = Guard.Against.Null(httpClient);
    private readonly KeycloakOptions _options = Guard.Against.Null(options).Value;
    private readonly ILogger<KeycloakDirectoryClient> _logger = logger;
    private readonly KeycloakRoleClient _roleClient = new(httpClient, logger);

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

        using var document = await response.ReadJsonAsync(cancellationToken);
        var root = document.RootElement;

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
    public async Task<string> CreateUserAsync(
        string email, string firstName, string lastName, string username,
        CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrWhiteSpace(email);
        Guard.Against.NullOrWhiteSpace(username);

        var payload = new
        {
            username,
            email,
            firstName = firstName ?? string.Empty,
            lastName = lastName ?? string.Empty,
            enabled = true,
            emailVerified = false
        };

        var response = await _httpClient.PostAsJsonAsync("users", payload, cancellationToken);
        response.EnsureSuccessStatusCode();

        return ExtractUserIdFromLocationHeader(response);
    }

    /// <inheritdoc />
    public async Task<string> CreateUserWithTempPasswordAsync(
        string email, string firstName, string lastName, string username, string tempPassword,
        CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrWhiteSpace(email);
        Guard.Against.NullOrWhiteSpace(username);
        Guard.Against.NullOrWhiteSpace(tempPassword);

        var payload = new
        {
            username,
            email,
            firstName = firstName ?? string.Empty,
            lastName = lastName ?? string.Empty,
            enabled = true,
            emailVerified = false,
            credentials = new[]
            {
                new
                {
                    type = "password",
                    value = tempPassword,
                    temporary = true
                }
            }
        };

        var response = await _httpClient.PostAsJsonAsync("users", payload, cancellationToken);
        response.EnsureSuccessStatusCode();

        return ExtractUserIdFromLocationHeader(response);
    }

    /// <inheritdoc />
    public Task AssignRealmRoleAsync(string userId, string roleName, CancellationToken cancellationToken = default) =>
        _roleClient.AssignRealmRoleAsync(userId, roleName, cancellationToken);

    /// <inheritdoc />
    public Task RevokeRealmRoleAsync(string userId, string roleName, CancellationToken cancellationToken = default) =>
        _roleClient.RevokeRealmRoleAsync(userId, roleName, cancellationToken);

    /// <inheritdoc />
    public async Task ResetTemporaryPasswordAsync(string userId, string tempPassword, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrWhiteSpace(userId);
        Guard.Against.NullOrWhiteSpace(tempPassword);

        var payload = new
        {
            type = "password",
            value = tempPassword,
            temporary = true
        };

        var response = await _httpClient.PutAsJsonAsync(
            $"users/{Uri.EscapeDataString(userId)}/reset-password", payload, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <inheritdoc />
    public async Task UpdateUserNameAsync(string userId, string firstName, string lastName, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrWhiteSpace(userId);

        var payload = new
        {
            firstName = firstName ?? string.Empty,
            lastName = lastName ?? string.Empty
        };

        var response = await _httpClient.PutAsJsonAsync(
            $"users/{Uri.EscapeDataString(userId)}", payload, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <inheritdoc />
    public async Task<List<ExternalUser>> GetUsersPagedAsync(int first, int max, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"users?first={first}&max={max}", cancellationToken);
        response.EnsureSuccessStatusCode();

        using var doc = await response.ReadJsonAsync(cancellationToken);

        var users = new List<ExternalUser>();
        foreach (var element in doc.RootElement.EnumerateArray())
        {
            users.Add(new ExternalUser(
                Id: element.GetProperty("id").GetString() ?? string.Empty,
                Username: element.GetProperty("username").GetString() ?? string.Empty,
                Email: element.TryGetProperty("email", out var emailProp) ? emailProp.GetString() : null,
                FirstName: element.TryGetProperty("firstName", out var fnProp) ? fnProp.GetString() : null,
                LastName: element.TryGetProperty("lastName", out var lnProp) ? lnProp.GetString() : null,
                Enabled: element.TryGetProperty("enabled", out var enabledProp) && enabledProp.GetBoolean()));
        }

        return users;
    }

    /// <inheritdoc />
    public Task<List<string>> GetUserRealmRolesAsync(string userId, CancellationToken cancellationToken = default) =>
        _roleClient.GetUserRealmRolesAsync(userId, cancellationToken);

    private static string ExtractUserIdFromLocationHeader(HttpResponseMessage response)
    {
        var location = response.Headers.Location?.ToString()
            ?? throw new InvalidOperationException("Keycloak did not return a Location header after user creation.");

        return location.Split('/').Last();
    }

    private string BuildUserLookupUrl(string userId)
    {
        var template = _options.UserLookupTemplate;
        if (!string.IsNullOrWhiteSpace(template))
        {
            template = template.Replace("{realm}", _options.Realm, StringComparison.OrdinalIgnoreCase);
            template = template.Replace("{0}", Uri.EscapeDataString(userId), StringComparison.OrdinalIgnoreCase);
            return template;
        }

        return $"/admin/realms/{_options.Realm}/users/{Uri.EscapeDataString(userId)}";
    }

    public async Task DeleteUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        // Honor UserLookupTemplate from KeycloakOptions so a custom realm path applies
        // to lookup AND delete consistently. Previously hardcoded — silent breakage when
        // an operator overrode the lookup template for a non-default Keycloak deployment.
        var url = BuildUserLookupUrl(userId);
        var response = await _httpClient.DeleteAsync(url, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Keycloak user {UserId} not found during delete (may have been already removed)", userId);
            return;
        }
        response.EnsureSuccessStatusCode();
        _logger.LogInformation("Deleted Keycloak user {UserId}", userId);
    }
}

