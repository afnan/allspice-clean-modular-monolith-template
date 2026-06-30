using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;
using Ardalis.Result;
using Mediator;

namespace AllSpice.CleanModularMonolith.Identity.Application.Features.Authorization.Commands.SetRolePermissions;

public sealed class SetRolePermissionsCommandHandler(
    IRoleRepository roleRepository,
    IRolePermissionRepository rolePermissionRepository,
    IPermissionRepository permissionRepository,
    IAuthzMapVersionRepository versionRepository,
    IAuthzCacheInvalidator cacheInvalidator)
    : IRequestHandler<SetRolePermissionsCommand, Result>
{
    private readonly IRoleRepository _roleRepository = roleRepository;
    private readonly IRolePermissionRepository _rolePermissionRepository = rolePermissionRepository;
    private readonly IPermissionRepository _permissionRepository = permissionRepository;
    private readonly IAuthzMapVersionRepository _versionRepository = versionRepository;
    private readonly IAuthzCacheInvalidator _cacheInvalidator = cacheInvalidator;

    public async ValueTask<Result> Handle(SetRolePermissionsCommand command, CancellationToken cancellationToken)
    {
        var role = await _roleRepository.GetByKeyAsync(command.RoleKey, cancellationToken);

        if (role is null)
        {
            return Result.NotFound($"Role '{command.RoleKey}' not found.");
        }

        // Remove existing mappings.
        var existing = await _rolePermissionRepository.ListByRoleIdAsync(role.Id, cancellationToken);
        _rolePermissionRepository.RemoveRange(existing);

        // Resolve each requested key; any unknown key aborts the operation.
        var newMappings = new List<RolePermission>(command.PermissionKeys.Count);
        foreach (var key in command.PermissionKeys)
        {
            var permission = await _permissionRepository.GetByKeyAsync(key, cancellationToken);
            if (permission is null)
            {
                return Result.Invalid(new ValidationError(
                    nameof(SetRolePermissionsCommand.PermissionKeys),
                    $"Permission key '{key}' does not exist."));
            }

            newMappings.Add(RolePermission.Create(role.Id, permission.Id));
        }

        foreach (var mapping in newMappings)
        {
            _rolePermissionRepository.Add(mapping);
        }

        (await _versionRepository.GetTrackedAsync(cancellationToken)).Bump();
        await _cacheInvalidator.InvalidateAsync(cancellationToken);
        return Result.Success();
    }
}
