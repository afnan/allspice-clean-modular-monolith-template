using System.Net;
using System.Net.Http.Json;
using Ardalis.GuardClauses;
using Microsoft.Extensions.Logging;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Services;

/// <summary>
/// Keycloak realm-role operations (assign/revoke/list, plus the get-or-create lookup) split out of
/// <see cref="KeycloakDirectoryClient"/> to keep that class focused on user operations. Shares the
/// directory client's configured <see cref="HttpClient"/>.
/// </summary>
internal sealed class KeycloakRoleClient(HttpClient httpClient, ILogger logger)
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ILogger _logger = logger;

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

    public async Task<List<string>> GetUserRealmRolesAsync(string userId, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrWhiteSpace(userId);

        var response = await _httpClient.GetAsync(
            $"users/{Uri.EscapeDataString(userId)}/role-mappings/realm", cancellationToken);
        response.EnsureSuccessStatusCode();

        using var doc = await response.ReadJsonAsync(cancellationToken);

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

        using var doc = await response.ReadJsonAsync(cancellationToken);
        var root = doc.RootElement;

        return new
        {
            id = root.GetProperty("id").GetString(),
            name = root.GetProperty("name").GetString()
        };
    }
}
