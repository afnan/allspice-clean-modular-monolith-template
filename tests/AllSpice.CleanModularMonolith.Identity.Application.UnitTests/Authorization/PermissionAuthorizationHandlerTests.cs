using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using Microsoft.AspNetCore.Authorization;
using Moq;
using Xunit;

namespace AllSpice.CleanModularMonolith.Identity.Application.UnitTests.Authorization;

public sealed class PermissionAuthorizationHandlerTests
{
    private static async Task<bool> Evaluate(bool userHasIt)
    {
        var perms = new Mock<ICurrentUserPermissions>();
        perms.Setup(p => p.HasPermissionAsync("authz.read", It.IsAny<CancellationToken>())).ReturnsAsync(userHasIt);
        var handler = new PermissionAuthorizationHandler(perms.Object);
        var requirement = new PermissionRequirement("authz.read");
        var context = new AuthorizationHandlerContext([requirement], user: new System.Security.Claims.ClaimsPrincipal(), resource: null);
        await handler.HandleAsync(context);
        return context.HasSucceeded;
    }

    [Fact] public async Task Succeeds_when_user_has_permission() => Assert.True(await Evaluate(true));
    [Fact] public async Task Fails_when_user_lacks_permission() => Assert.False(await Evaluate(false));
}
