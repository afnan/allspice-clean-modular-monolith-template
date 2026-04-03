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
public sealed class KeycloakDirectoryClient : IExternalDirectoryClient
{
    private readonly HttpClient _httpClient;
    private readonly KeycloakOptions _options;
    private readonly ILogger<KeycloakDirectoryClient> _logger;

    public KeycloakDirectoryClient(
        HttpClient httpClient,
        IOptions<KeycloakOptions> options,
        ILogger<KeycloakDirectoryClient> logger)
    {
        Guard.Against.Null(httpClient);
        Guard.Against.Null(options);

        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
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
    public async Task AssignRealmRoleAsync(string userId, string roleName, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrWhiteSpace(userId);
        Guard.Against.NullOrWhiteSpace(roleName);

        var role = await GetOrCreateRealmRoleAsync(roleName, cancellationToken);

        var payload = new[] { role };
        var response = await _httpClient.PostAsJsonAsync(
            $"users/{Uri.EscapeDataString(userId)}/role-mappings/realm", payload, cancellationToken);
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Assigned realm role {Role} to user {UserId}", roleName, userId);
    }

    /// <inheritdoc />
    public async Task RevokeRealmRoleAsync(string userId, string roleName, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrWhiteSpace(userId);
        Guard.Against.NullOrWhiteSpace(roleName);

        var role = await GetRealmRoleByNameAsync(roleName, cancellationToken);
        if (role is null)
        {
            _logger.LogWarning("Cannot revoke role {Role} from user {UserId}: role not found", roleName, userId);
            return;
        }

        var request = new HttpRequestMessage(HttpMethod.Delete,
            $"users/{Uri.EscapeDataString(userId)}/role-mappings/realm")
        {
            Content = JsonContent.Create(new[] { role })
        };

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Revoked realm role {Role} from user {UserId}", roleName, userId);
    }

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

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

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
    public async Task<List<string>> GetUserRealmRolesAsync(string userId, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrWhiteSpace(userId);

        var response = await _httpClient.GetAsync(
            $"users/{Uri.EscapeDataString(userId)}/role-mappings/realm", cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        return doc.RootElement.EnumerateArray()
            .Select(r => r.GetProperty("name").GetString() ?? string.Empty)
            .Where(name => !string.IsNullOrEmpty(name))
            .ToList();
    }

    private async Task<object> GetOrCreateRealmRoleAsync(string roleName, CancellationToken cancellationToken)
    {
        var existing = await GetRealmRoleByNameAsync(roleName, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var createPayload = new { name = roleName };
        var createResponse = await _httpClient.PostAsJsonAsync("roles", createPayload, cancellationToken);
        createResponse.EnsureSuccessStatusCode();

        _logger.LogInformation("Created realm role {Role}", roleName);

        return await GetRealmRoleByNameAsync(roleName, cancellationToken)
            ?? throw new InvalidOperationException($"Failed to retrieve realm role '{roleName}' after creation.");
    }

    private async Task<object?> GetRealmRoleByNameAsync(string roleName, CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync(
            $"roles/{Uri.EscapeDataString(roleName)}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = doc.RootElement;

        return new
        {
            id = root.GetProperty("id").GetString(),
            name = root.GetProperty("name").GetString()
        };
    }

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
}

