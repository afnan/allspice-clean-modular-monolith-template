using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;

namespace AllSpice.CleanModularMonolith.Identity.Application.UnitTests.Authorization;

public sealed class PermissionKeyTests
{
    [Theory]
    [InlineData("authz.read", true)]
    [InlineData("cms:articles.publish", true)]
    [InlineData("cms.access", true)]
    [InlineData("Has Space", false)]
    [InlineData("", false)]
    [InlineData("UPPER", false)]
    public void IsValid_enforces_lowercase_namespaced_format(string key, bool expected)
        => Assert.Equal(expected, PermissionKey.IsValid(key));
}
