using System.Security.Claims;
using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Authorization;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Authorization;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.IntegrationTests.Helpers;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.IntegrationTests.Authorization;

public sealed class PermissionEnforcementTests
{
    private static async Task<bool> EvaluateAsync(string[] roles)
    {
        // Arrange: a SQLite Identity DB seeded with qa-admin -> authz.read, shared with the DI graph below.
        // The shared-connection harness ensures the seed context and the DI-resolved PermissionMapStore
        // read and write to the same SQLite :memory: connection — so the store sees the seeded rows.
        await using var harness = await TestIdentityDbContextFactory.CreateSharedAsync();
        var role = Role.Create("qa-admin", null);
        var perm = Permission.Create("authz.read", "Read authz", isSystem: true);
        harness.Context.Roles.Add(role);
        harness.Context.Permissions.Add(perm);
        harness.Context.RolePermissions.Add(RolePermission.Create(role.Id, perm.Id));
        harness.Context.AuthzMapVersions.Add(AuthzMapVersion.Initial());
        await harness.Context.SaveChangesAsync();

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            roles.Select(r => new Claim(ClaimTypes.Role, r)), "test", ClaimTypes.Name, ClaimTypes.Role));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();
        harness.RegisterDbContext(services); // registers IdentityDbContext over the SAME open connection
        services.AddScoped<IPermissionMapStore, PermissionMapStore>();
        services.AddSingleton<IPermissionMapCache, PermissionMapCache>();
        services.AddSingleton<IHttpContextAccessor>(
            new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } });
        services.AddScoped<ICurrentUserPermissions, CurrentUserPermissions>();
        await using var provider = services.BuildServiceProvider();

        await using var scope = provider.CreateAsyncScope();
        var handler = new PermissionAuthorizationHandler(
            scope.ServiceProvider.GetRequiredService<ICurrentUserPermissions>());
        var context = new AuthorizationHandlerContext(
            [new PermissionRequirement("authz.read")], principal, resource: null);
        await handler.HandleAsync(context);
        return context.HasSucceeded;
    }

    [Fact] public async Task Mapped_role_is_authorized() => Assert.True(await EvaluateAsync(["qa-admin"]));
    [Fact] public async Task Unmapped_role_is_denied()   => Assert.False(await EvaluateAsync(["bob"]));
    [Fact] public async Task No_roles_is_denied()        => Assert.False(await EvaluateAsync([]));
}
