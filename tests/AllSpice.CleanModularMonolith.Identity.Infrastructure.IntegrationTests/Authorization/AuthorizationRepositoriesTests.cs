using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.IntegrationTests.Helpers;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Repositories;
using Xunit;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.IntegrationTests.Authorization;

public sealed class AuthorizationRepositoriesTests
{
    [Fact]
    public async Task Permission_round_trips_by_key()
    {
        await using var ctx = await TestIdentityDbContextFactory.CreateAsync();
        var repo = new PermissionRepository(ctx);
        await repo.AddAsync(Permission.Create("cms.access", "Access CMS", true), default);
        await ctx.SaveChangesAsync();

        var found = await repo.GetByKeyAsync("cms.access", default);
        Assert.NotNull(found);
        Assert.True(found!.IsSystem);
    }

    [Fact]
    public async Task Version_repo_creates_and_bumps_singleton()
    {
        await using var ctx = await TestIdentityDbContextFactory.CreateAsync();
        var repo = new AuthzMapVersionRepository(ctx);
        var v = await repo.GetTrackedAsync(default);
        v.Bump();
        await ctx.SaveChangesAsync();

        var reloaded = await new AuthzMapVersionRepository(ctx).GetTrackedAsync(default);
        Assert.Equal(1, reloaded.Version);
    }
}
