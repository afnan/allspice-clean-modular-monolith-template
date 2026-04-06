using Microsoft.Extensions.Options;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Options;

public sealed class KeycloakOptionsValidator : IValidateOptions<KeycloakOptions>
{
    public ValidateOptionsResult Validate(string? name, KeycloakOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ServiceName) && string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            return ValidateOptionsResult.Fail(
                "Either ServiceName or BaseUrl must be provided for Keycloak configuration.");
        }

        return ValidateOptionsResult.Success;
    }
}
