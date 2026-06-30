using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Xunit;

namespace AllSpice.CleanModularMonolith.Identity.Application.UnitTests.Authorization;

public sealed class PermissionPolicyProviderTests
{
    private static PermissionPolicyProvider NewProvider()
        => new(Options.Create(new AuthorizationOptions()));

    [Fact]
    public async Task Materializes_a_policy_for_a_perm_prefixed_name()
    {
        var policy = await NewProvider().GetPolicyAsync("perm:authz.read");
        Assert.NotNull(policy);
        Assert.Contains(policy!.Requirements, r => r is PermissionRequirement pr && pr.PermissionKey == "authz.read");
    }

    [Fact]
    public async Task Delegates_non_perm_policies_to_the_default_provider()
        => Assert.Null(await NewProvider().GetPolicyAsync("authenticated")); // not registered here -> default returns null
}
