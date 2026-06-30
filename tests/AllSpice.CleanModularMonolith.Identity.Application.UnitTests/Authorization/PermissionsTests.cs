using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using Xunit;

namespace AllSpice.CleanModularMonolith.Identity.Application.UnitTests.Authorization;

public sealed class PermissionsTests
{
    [Theory]
    [InlineData("authz.read", true)]
    [InlineData("cms:articles.publish", true)]
    [InlineData("cms.access", true)]
    [InlineData("Has Space", false)]
    [InlineData("", false)]
    [InlineData("UPPER", false)]
    public void IsValidKey_enforces_lowercase_namespaced_format(string key, bool expected)
        => Assert.Equal(expected, Permissions.IsValidKey(key));

    [Fact]
    public void All_contains_the_seeded_system_keys()
    {
        Assert.Contains(Permissions.AuthzRead, Permissions.All);
        Assert.Contains(Permissions.AuthzManage, Permissions.All);
    }
}
