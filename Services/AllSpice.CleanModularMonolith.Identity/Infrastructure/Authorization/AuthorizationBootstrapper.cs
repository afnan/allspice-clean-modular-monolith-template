using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AuthzOptions = AllSpice.CleanModularMonolith.Identity.Infrastructure.Options.AuthorizationOptions;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Authorization;

public sealed class AuthorizationBootstrapper(
    IOptions<AuthzOptions> options,
    IRoleRepository roleRepository,
    IPermissionRepository permissionRepository,
    IRolePermissionRepository rolePermissionRepository,
    IAuthzMapVersionRepository versionRepository,
    ILogger<AuthorizationBootstrapper> logger)
{
    private readonly IOptions<AuthzOptions> _options = options;
    private readonly IRoleRepository _roleRepository = roleRepository;
    private readonly IPermissionRepository _permissionRepository = permissionRepository;
    private readonly IRolePermissionRepository _rolePermissionRepository = rolePermissionRepository;
    private readonly IAuthzMapVersionRepository _versionRepository = versionRepository;
    private readonly ILogger<AuthorizationBootstrapper> _logger = logger;

    private static readonly string[] AdminKeys = [Permissions.AuthzRead, Permissions.AuthzManage];

    public async Task BootstrapAsync(CancellationToken cancellationToken)
    {
        var roleKey = _options.Value.BootstrapAdminRole;
        if (string.IsNullOrWhiteSpace(roleKey))
        {
            return;
        }

        var role = await _roleRepository.GetByKeyAsync(roleKey, cancellationToken);
        if (role is null)
        {
            role = Role.Create(roleKey, "Bootstrap admin role");
            await _roleRepository.AddAsync(role, cancellationToken);
            _logger.LogWarning(
                "Bootstrap admin role '{Role}' was not synced from Keycloak; created locally. Grants are inert until a user holds this realm role.",
                roleKey);
        }

        var existing = (await _rolePermissionRepository.ListByRoleIdAsync(role.Id, cancellationToken))
            .Select(rp => rp.PermissionId)
            .ToHashSet();

        var changed = false;
        foreach (var key in AdminKeys)
        {
            var permission = await _permissionRepository.GetByKeyAsync(key, cancellationToken);
            if (permission is not null && !existing.Contains(permission.Id))
            {
                _rolePermissionRepository.Add(RolePermission.Create(role.Id, permission.Id));
                changed = true;
            }
        }

        if (changed)
        {
            (await _versionRepository.GetTrackedAsync(cancellationToken)).Bump();
        }
    }
}
