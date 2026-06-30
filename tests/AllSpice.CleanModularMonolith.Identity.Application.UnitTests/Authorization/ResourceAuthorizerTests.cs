using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Authorization;
using AllSpice.CleanModularMonolith.SharedKernel.Identity;
using Ardalis.Result;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AllSpice.CleanModularMonolith.Identity.Application.UnitTests.Authorization;

public sealed class ResourceAuthorizerTests
{
    private sealed record OwnedThing(Guid OwnerId);

    private sealed class OwnedThingRule : AuthorizationHandler<OperationAuthorizationRequirement, OwnedThing>
    {
        private readonly IAuthorizationContext _ctx;
        public OwnedThingRule(IAuthorizationContext ctx) => _ctx = ctx;
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext c, OperationAuthorizationRequirement r, OwnedThing resource)
        {
            if (resource.OwnerId == _ctx.UserId) c.Succeed(r);
            return Task.CompletedTask;
        }
    }

    private static IResourceAuthorizer Build(Guid currentUser)
    {
        var services = new ServiceCollection();
        services.AddLogging(); // DefaultAuthorizationService requires ILogger in .NET 10
        services.AddAuthorizationCore();
        services.AddHttpContextAccessor(); // ResourceAuthorizer depends on IHttpContextAccessor
        var userCtx = new CurrentUserContext();
        userCtx.Resolve(currentUser);
        services.AddSingleton<ICurrentUserContext>(userCtx);
        services.AddScoped<IAuthorizationContext, AuthorizationContext>();
        services.AddSingleton<IAuthorizationHandler>(sp => new OwnedThingRule(sp.GetRequiredService<IAuthorizationContext>()));
        services.AddScoped<IResourceAuthorizer, ResourceAuthorizer>();
        return services.BuildServiceProvider().GetRequiredService<IResourceAuthorizer>();
    }

    [Fact]
    public async Task Owner_is_authorized()
    {
        var me = Guid.NewGuid();
        var result = await Build(me).AuthorizeAsync(new OwnedThing(me), AuthorizationActions.Update, default);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Non_owner_is_forbidden()
    {
        var result = await Build(Guid.NewGuid()).AuthorizeAsync(new OwnedThing(Guid.NewGuid()), AuthorizationActions.Update, default);
        Assert.Equal(ResultStatus.Forbidden, result.Status);
    }
}
