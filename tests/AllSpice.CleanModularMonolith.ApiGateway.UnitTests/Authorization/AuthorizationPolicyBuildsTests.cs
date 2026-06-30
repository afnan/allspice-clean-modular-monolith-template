using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace AllSpice.CleanModularMonolith.ApiGateway.UnitTests.Authorization;

public sealed class AuthorizationPolicyBuildsTests
{
    private static IAuthorizationPolicyProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddAuthorization(options =>
        {
            options.AddPolicy("authenticated", p => p.RequireAssertion(_ => true));
            options.AddPolicy("allow-anonymous", p => p.RequireAssertion(_ => true));
        });
        services.AddPermissionAuthorization();
        return services.BuildServiceProvider().GetRequiredService<IAuthorizationPolicyProvider>();
    }

    [Fact]
    public async Task Gateway_core_policies_still_resolve_after_module_roles_removal()
    {
        var policyProvider = BuildProvider();

        Assert.NotNull(await policyProvider.GetPolicyAsync("authenticated"));
        Assert.NotNull(await policyProvider.GetPolicyAsync("allow-anonymous"));
    }

    [Fact]
    public async Task Permission_policy_materializes_with_PermissionRequirement()
    {
        var policyProvider = BuildProvider();

        var policy = await policyProvider.GetPolicyAsync("perm:authz.read");

        Assert.NotNull(policy);
        Assert.Contains(policy!.Requirements, r => r is PermissionRequirement pr && pr.PermissionKey == "authz.read");
    }
}
