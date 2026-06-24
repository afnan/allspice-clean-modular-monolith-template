using System.Security.Claims;
using System.Text.Json;
using Ardalis.GuardClauses;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace AllSpice.CleanModularMonolith.Identity.Abstractions.Authentication;

/// <summary>
/// Provides extension methods for configuring multi-portal authentication schemes.
/// </summary>
public static class IdentityPortalAuthenticationBuilderExtensions
{
    /// <summary>
    /// Registers JWT bearer handlers for ERP/public portals using Keycloak and configures default schemes.
    /// </summary>
    /// <param name="builder">The ASP.NET authentication builder.</param>
    /// <param name="configure">Callback used to configure portal options.</param>
    /// <returns>The same authentication builder to support chaining.</returns>
    public static AuthenticationBuilder AddIdentityPortals(this AuthenticationBuilder builder, Action<IdentityPortalOptions> configure)
    {
        Guard.Against.Null(builder);
        Guard.Against.Null(configure);

        var portalOptions = new IdentityPortalOptions();
        configure(portalOptions);

        Guard.Against.NullOrWhiteSpace(portalOptions.ErpAuthority, nameof(portalOptions.ErpAuthority));
        Guard.Against.NullOrWhiteSpace(portalOptions.ErpAudience, nameof(portalOptions.ErpAudience));

        builder.AddJwtBearer(portalOptions.ErpScheme, options =>
        {
            options.Authority = portalOptions.ErpAuthority;
            options.Audience = portalOptions.ErpAudience;
            options.RequireHttpsMetadata = portalOptions.RequireHttpsMetadata;
            options.MapInboundClaims = false;
            options.TokenValidationParameters = CreateDefaultParameters();
            options.Events = CreateBearerEvents();
        });

        if (!string.IsNullOrWhiteSpace(portalOptions.PublicAuthority) &&
            !string.IsNullOrWhiteSpace(portalOptions.PublicAudience))
        {
            builder.AddJwtBearer(portalOptions.PublicScheme, options =>
            {
                options.Authority = portalOptions.PublicAuthority;
                options.Audience = portalOptions.PublicAudience;
                options.RequireHttpsMetadata = portalOptions.RequireHttpsMetadata;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = CreateDefaultParameters();
                options.Events = CreateBearerEvents();
            });
        }

        builder.Services.Configure<AuthenticationOptions>(options =>
        {
            options.DefaultAuthenticateScheme = portalOptions.ErpScheme;
            options.DefaultChallengeScheme = portalOptions.UsePublicAsDefaultChallenge && !string.IsNullOrWhiteSpace(portalOptions.PublicAuthority)
                ? portalOptions.PublicScheme
                : portalOptions.ErpScheme;
        });

        return builder;
    }

    /// <summary>Hub path prefix whose connections may carry the JWT as a query-string token.</summary>
    private const string HubsPathPrefix = "/hubs";

    /// <summary>
    /// Builds the shared <see cref="JwtBearerEvents"/> for each portal scheme:
    /// <list type="bullet">
    /// <item><c>OnMessageReceived</c> reads the JWT from the <c>access_token</c> query parameter for SignalR
    /// hub paths — browsers cannot set the <c>Authorization</c> header on WebSocket/SSE, so without this every
    /// authenticated hub connection is rejected with 401.</item>
    /// <item><c>OnTokenValidated</c> flattens Keycloak realm roles (<c>realm_access.roles</c>) into standard
    /// <see cref="ClaimTypes.Role"/> claims so <c>[Authorize(Roles = …)]</c> works.</item>
    /// </list>
    /// </summary>
    private static JwtBearerEvents CreateBearerEvents() =>
        new()
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) &&
                    context.Request.Path.StartsWithSegments(HubsPathPrefix))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                MapRealmRolesToRoleClaims(context.Principal);
                return Task.CompletedTask;
            }
        };

    /// <summary>
    /// Adds a <see cref="ClaimTypes.Role"/> claim for each Keycloak realm role found in the principal's
    /// <c>realm_access</c> claim (JSON <c>{ "roles": [ … ] }</c>). Malformed JSON and non-string role
    /// entries are skipped, so a misconfigured protocol mapper can never fault token validation.
    /// </summary>
    private static void MapRealmRolesToRoleClaims(ClaimsPrincipal? principal)
    {
        if (principal?.Identity is not ClaimsIdentity identity)
        {
            return;
        }

        var realmAccess = principal.FindFirst("realm_access")?.Value;
        if (string.IsNullOrWhiteSpace(realmAccess))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(realmAccess);
            if (!document.RootElement.TryGetProperty("roles", out var roles) || roles.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (var role in roles.EnumerateArray())
            {
                // Skip non-string entries: GetString() throws InvalidOperationException on numbers/objects/etc.
                if (role.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var roleName = role.GetString();
                if (!string.IsNullOrWhiteSpace(roleName) && !identity.HasClaim(ClaimTypes.Role, roleName))
                {
                    identity.AddClaim(new Claim(ClaimTypes.Role, roleName));
                }
            }
        }
        catch (JsonException)
        {
            // Malformed realm_access — leave role claims untouched.
        }
    }

    /// <summary>
    /// Creates the default token validation parameters applied to each portal scheme.
    /// </summary>
    /// <returns>A configured <see cref="TokenValidationParameters"/> instance.</returns>
    private static TokenValidationParameters CreateDefaultParameters() =>
        new()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            // Keycloak emits the human username as `preferred_username` (with MapInboundClaims = false the
            // claim keeps that name), so User.Identity.Name resolves to the username rather than the GUID sub.
            NameClaimType = "preferred_username",
            RoleClaimType = ClaimTypes.Role
        };
}


