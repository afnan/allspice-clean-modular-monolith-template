using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;

/// <summary>
/// Provides DI helpers for authorization policies.
/// </summary>
public static class AuthorizationServiceCollectionExtensions
{
    /// <summary>Registers the permission policy provider + handler. The resolver, cache, and map store are
    /// registered by the Identity module; this wires the ASP.NET authorization plumbing in the host.</summary>
    public static IServiceCollection AddPermissionAuthorization(this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        // Scoped is correct: PermissionAuthorizationHandler depends on the scoped ICurrentUserPermissions,
        // and the authorization middleware resolves handlers per request — this is not a captive dependency.
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        return services;
    }
}
