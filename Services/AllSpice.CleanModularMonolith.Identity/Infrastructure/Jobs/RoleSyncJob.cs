using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Options;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Jobs;

/// <summary>
/// Upserts local <see cref="Role"/> rows from Keycloak realm roles so the app's role→permission
/// mapping always covers every role that exists in the IdP. Degrades gracefully when Keycloak
/// is not yet linked: the job short-circuits before creating any DI scope.
/// </summary>
[DisallowConcurrentExecution]
public sealed class RoleSyncJob(
    IServiceScopeFactory scopeFactory,
    IOptions<KeycloakOptions> keycloakOptions,
    ILogger<RoleSyncJob> logger) : IJob
{
    public const string JobIdentity = "RoleSyncJob";

    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly IOptions<KeycloakOptions> _keycloakOptions = keycloakOptions;
    private readonly ILogger<RoleSyncJob> _logger = logger;

    public async Task Execute(IJobExecutionContext context)
    {
        if (!_keycloakOptions.Value.IsAdminConfigured)
        {
            _logger.LogDebug("Keycloak is not linked yet — skipping role sync.");
            return;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var client = scope.ServiceProvider.GetRequiredService<KeycloakRoleClient>();
        var roleRepo = scope.ServiceProvider.GetRequiredService<IRoleRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        try
        {
            var realmRoles = await client.GetAllRealmRolesAsync(context.CancellationToken);
            foreach (var roleName in realmRoles)
            {
                if (await roleRepo.GetByKeyAsync(roleName, context.CancellationToken) is null)
                {
                    await roleRepo.AddAsync(Role.Create(roleName, null), context.CancellationToken);
                }
            }

            await dbContext.SaveChangesAsync(context.CancellationToken);
            _logger.LogInformation("Synced {Count} realm roles", realmRoles.Count);
        }
        catch (Exception ex) when (ex is HttpRequestException or TimeoutException)
        {
            _logger.LogWarning(ex, "Role sync failed transiently; will retry on next trigger.");
        }
    }
}
