using AllSpice.CleanModularMonolith.Identity.Infrastructure.Jobs;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Quartz;

namespace AllSpice.CleanModularMonolith.Identity.Application.UnitTests.Authorization;

public sealed class RoleSyncJobTests
{
    [Fact]
    public async Task Does_nothing_when_keycloak_is_not_configured()
    {
        var scopeFactory = new Mock<IServiceScopeFactory>(MockBehavior.Strict);
        var keycloakOptions = Options.Create(new KeycloakOptions { Realm = "demo" }); // not admin-configured

        var job = new RoleSyncJob(scopeFactory.Object, keycloakOptions, NullLogger<RoleSyncJob>.Instance);
        await job.Execute(new Mock<IJobExecutionContext>().Object);

        scopeFactory.Verify(f => f.CreateScope(), Times.Never);
    }
}
