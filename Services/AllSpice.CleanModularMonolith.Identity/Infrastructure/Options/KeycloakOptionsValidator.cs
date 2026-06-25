using Microsoft.Extensions.Options;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Options;

public sealed class KeycloakOptionsValidator : IValidateOptions<KeycloakOptions>
{
    public ValidateOptionsResult Validate(string? name, KeycloakOptions options)
    {
        // The IdP is linked AFTER scaffolding, so a fully-empty Keycloak section is a valid "not linked yet"
        // state: the app boots and the Keycloak-dependent health checks report Degraded (see KeycloakOptions
        // .IsAdminConfigured) until it is configured. Only validate once the operator has started filling it
        // in — a partial section is almost certainly a misconfiguration and should fail fast at startup.
        var anyConfigured =
            !string.IsNullOrWhiteSpace(options.ServiceName)
            || !string.IsNullOrWhiteSpace(options.BaseUrl)
            || !string.IsNullOrWhiteSpace(options.Realm)
            || !string.IsNullOrWhiteSpace(options.ApiToken)
            || !string.IsNullOrWhiteSpace(options.ClientId)
            || !string.IsNullOrWhiteSpace(options.ClientSecret);

        if (!anyConfigured)
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.ServiceName) && string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            failures.Add("Identity:Keycloak — either ServiceName or BaseUrl must be provided.");
        }

        if (string.IsNullOrWhiteSpace(options.Realm))
        {
            failures.Add("Identity:Keycloak — Realm is required.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
