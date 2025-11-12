using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;

/// <summary>
/// Provides DI helpers for module-based authorization policies.
/// </summary>
public static class AuthorizationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the module role authorization handler required for module policies.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The original service collection for chaining.</returns>
    public static IServiceCollection AddModuleRoleAuthorization(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IAuthorizationHandler, ModuleRoleAuthorizationHandler>();
        return services;
    }

    /// <summary>
    /// Adds a policy that requires the authenticated user to have the specified module role.
    /// </summary>
    /// <param name="options">The authorization options.</param>
    /// <param name="policyName">Unique policy name.</param>
    /// <param name="moduleKey">Module identifier.</param>
    /// <param name="roleKey">Role within the module.</param>
    /// <returns>The authorization options for chaining.</returns>
    public static AuthorizationOptions AddModuleRolePolicy(this AuthorizationOptions options, string policyName, string moduleKey, string roleKey)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.AddPolicy(policyName, builder =>
        {
            builder.RequireAuthenticatedUser();
            builder.AddRequirements(new ModuleRoleRequirement(moduleKey, roleKey));
        });

        return options;
    }
}


