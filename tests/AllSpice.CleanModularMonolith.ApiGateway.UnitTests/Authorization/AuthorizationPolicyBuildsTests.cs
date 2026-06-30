using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AllSpice.CleanModularMonolith.ApiGateway.UnitTests.Authorization;

public sealed class AuthorizationPolicyBuildsTests
{
    [Fact]
    public async Task Gateway_core_policies_still_resolve_after_module_roles_removal()
    {
        var services = new ServiceCollection();
        services.AddAuthorization(options =>
        {
            options.AddPolicy("authenticated", p => p.RequireAssertion(_ => true));
            options.AddPolicy("allow-anonymous", p => p.RequireAssertion(_ => true));
        });
        var provider = services.BuildServiceProvider();
        var policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();

        Assert.NotNull(await policyProvider.GetPolicyAsync("authenticated"));
        Assert.NotNull(await policyProvider.GetPolicyAsync("allow-anonymous"));
    }
}
