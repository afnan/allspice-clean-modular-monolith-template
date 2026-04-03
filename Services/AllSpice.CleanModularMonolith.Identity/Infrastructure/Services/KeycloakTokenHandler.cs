namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Services;

/// <summary>
/// DelegatingHandler that injects a Bearer token from <see cref="KeycloakTokenProvider"/>
/// into outgoing HTTP requests to the Keycloak Admin API.
/// </summary>
public sealed class KeycloakTokenHandler : DelegatingHandler
{
    private readonly KeycloakTokenProvider _tokenProvider;

    public KeycloakTokenHandler(KeycloakTokenProvider tokenProvider)
    {
        _tokenProvider = tokenProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = await _tokenProvider.GetTokenAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await base.SendAsync(request, cancellationToken);
    }
}
