using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;

public static class AuthorizationServiceCollectionExtensions
{
    public static IServiceCollection AddModuleRoleAuthorization(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IAuthorizationHandler, ModuleRoleAuthorizationHandler>();
        return services;
    }

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


