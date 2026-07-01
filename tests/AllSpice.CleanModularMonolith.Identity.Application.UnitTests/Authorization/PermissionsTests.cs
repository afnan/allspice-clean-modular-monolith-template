using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;

namespace AllSpice.CleanModularMonolith.Identity.Application.UnitTests.Authorization;

public sealed class PermissionsTests
{
    [Fact]
    public void All_contains_the_seeded_system_keys()
    {
        Assert.Contains(Permissions.AuthzRead, Permissions.All);
        Assert.Contains(Permissions.AuthzManage, Permissions.All);
    }
}
