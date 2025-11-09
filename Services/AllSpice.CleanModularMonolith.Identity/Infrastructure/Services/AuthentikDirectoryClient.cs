using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AllSpice.CleanModularMonolith.Identity.Application.Contracts.External;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Options;
using Ardalis.GuardClauses;
using Microsoft.Extensions.Options;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Services;

public sealed class AuthentikDirectoryClient : IExternalDirectoryClient
{
    private readonly HttpClient _httpClient;
    private readonly AuthentikOptions _options;

    public AuthentikDirectoryClient(HttpClient httpClient, IOptions<AuthentikOptions> options)
    {
        Guard.Against.Null(httpClient);
        Guard.Against.Null(options);

        _httpClient = httpClient;
        _options = options.Value;
    }

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

        if (root.TryGetProperty("name", out var nameElement))
        {
            return nameElement.GetString();
        }

        if (root.TryGetProperty("username", out var usernameElement))
        {
            return usernameElement.GetString();
        }

        return userId;
    }

    public async Task InviteUserAsync(string email, string displayName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.InvitationEndpoint))
        {
            return;
        }

        Guard.Against.NullOrWhiteSpace(email);
        Guard.Against.NullOrWhiteSpace(displayName);

        var payload = new
        {
            email,
            name = displayName,
            send_email = true
        };

        var response = await _httpClient.PostAsJsonAsync(_options.InvitationEndpoint, payload, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private string BuildUserLookupUrl(string userId)
    {
        if (!string.IsNullOrWhiteSpace(_options.UserLookupTemplate) && _options.UserLookupTemplate.Contains("{0}", StringComparison.Ordinal))
        {
            return string.Format(_options.UserLookupTemplate, Uri.EscapeDataString(userId));
        }

        var basePath = string.IsNullOrWhiteSpace(_options.UserLookupTemplate)
            ? "/api/v3/core/users"
            : _options.UserLookupTemplate;

        return $"{basePath.TrimEnd('/')}/{Uri.EscapeDataString(userId)}/";
    }
}


