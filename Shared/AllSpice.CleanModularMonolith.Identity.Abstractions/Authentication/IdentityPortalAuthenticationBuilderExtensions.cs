using System.Security.Claims;
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
    /// Registers JWT bearer handlers for ERP/public portals and configures default schemes.
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
            options.MapInboundClaims = false;
            options.TokenValidationParameters = CreateDefaultParameters();
        });

        if (!string.IsNullOrWhiteSpace(portalOptions.PublicAuthority) &&
            !string.IsNullOrWhiteSpace(portalOptions.PublicAudience))
        {
            builder.AddJwtBearer(portalOptions.PublicScheme, options =>
            {
                options.Authority = portalOptions.PublicAuthority;
                options.Audience = portalOptions.PublicAudience;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = CreateDefaultParameters();
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
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role
        };
}


