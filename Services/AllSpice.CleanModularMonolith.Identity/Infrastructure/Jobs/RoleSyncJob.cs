using System.Text.Json;
using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Options;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Services;
using AllSpice.CleanModularMonolith.SharedKernel.Exceptions;
using Microsoft.EntityFrameworkCore;
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

            // Dedupe case-insensitively before the upsert loop: two realm role names that differ only
            // by case map to the same normalised key after Role.Create lowercases them, which would
            // stage duplicate rows against the unique index if both were processed.
            var uniqueRoleNames = realmRoles.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            var addedCount = 0;
            // Belt-and-suspenders: track keys added in this batch because GetByKeyAsync queries the
            // database and cannot see unsaved rows staged earlier in the same batch.
            var addedKeysInBatch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var roleName in uniqueRoleNames)
            {
                var normalizedKey = roleName.ToLowerInvariant();
                if (addedKeysInBatch.Contains(normalizedKey))
                {
                    continue;
                }

                if (await roleRepo.GetByKeyAsync(roleName, context.CancellationToken) is null)
                {
                    await roleRepo.AddAsync(Role.Create(roleName, null), context.CancellationToken);
                    addedKeysInBatch.Add(normalizedKey);
                    addedCount++;
                }
            }

            await dbContext.SaveChangesAsync(context.CancellationToken);
            _logger.LogInformation("Synced realm roles: {Added} new, {Total} total", addedCount, uniqueRoleNames.Count);
        }
        catch (Exception ex) when (ex is HttpRequestException or TimeoutException or JsonException or KeyNotFoundException or IdentityServerUnreachableException)
        {
            _logger.LogWarning(ex, "Role sync failed transiently; will retry on next trigger.");
        }
        catch (DbUpdateException ex)
        {
            // Another node's scheduler inserted the same realm role concurrently (unique index on Role.Key).
            // Benign: the role now exists, so the next trigger is a no-op. Don't fail the job.
            _logger.LogWarning(ex, "Role sync hit a concurrent insert; treating as benign and retrying next trigger.");
        }
    }
}
