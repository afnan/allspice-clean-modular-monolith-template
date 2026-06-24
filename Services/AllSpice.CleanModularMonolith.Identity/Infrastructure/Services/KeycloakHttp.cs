using System.Text.Json;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Services;

/// <summary>
/// Small HTTP helpers shared by the Keycloak Admin clients. Centralises the read-stream-then-parse-JSON
/// boilerplate that the directory and role clients otherwise repeat.
/// </summary>
internal static class KeycloakHttp
{
    /// <summary>
    /// Reads the response content as a stream and parses it into a <see cref="JsonDocument"/>. The caller
    /// owns the returned document (<c>await using</c>); the underlying stream is fully consumed and disposed here.
    /// </summary>
    public static async Task<JsonDocument> ReadJsonAsync(this HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }
}
