using AllSpice.CleanModularMonolith.Identity.Infrastructure.Jobs;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Quartz;

namespace AllSpice.CleanModularMonolith.Identity.Application.UnitTests;

/// <summary>
/// Before the IdP is linked the sync job must be genuinely idle — not fire, fail on a null base address, and
/// spam an error log + sync-issue row every cron tick. It short-circuits on
/// <see cref="KeycloakOptions.IsAdminConfigured"/> before touching DI.
/// </summary>
public class KeycloakUserSyncJobTests
{
    [Fact]
    public async Task Execute_creates_no_scope_and_does_nothing_when_keycloak_is_not_configured()
    {
        // Strict: the test fails if the job creates a DI scope (and thus resolves the directory client / makes
        // a Keycloak call). It must short-circuit first when the IdP isn't linked.
        var scopeFactory = new Mock<IServiceScopeFactory>(MockBehavior.Strict);
        var syncOptions = Options.Create(new IdentitySyncOptions());
        var keycloakOptions = Options.Create(new KeycloakOptions { Realm = "demo" }); // no base, no credentials

        var job = new KeycloakUserSyncJob(
            scopeFactory.Object, syncOptions, keycloakOptions, NullLogger<KeycloakUserSyncJob>.Instance);

        var context = new Mock<IJobExecutionContext>();

        await job.Execute(context.Object);

        scopeFactory.Verify(f => f.CreateScope(), Times.Never);
    }
}
