using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;
using AllSpice.CleanModularMonolith.SharedKernel.Behaviors;
using Ardalis.Result;
using Mediator;

namespace AllSpice.CleanModularMonolith.Identity.Application.Features.Authorization.Commands.SetRolePermissions;

public sealed class SetRolePermissionsCommandHandler(
    IRoleRepository roleRepository,
    IRolePermissionRepository rolePermissionRepository,
    IPermissionRepository permissionRepository,
    IAuthzMapVersionRepository versionRepository,
    IAuthzCacheInvalidator cacheInvalidator,
    IPostCommitActions postCommitActions)
    : IRequestHandler<SetRolePermissionsCommand, Result>
{
    private readonly IRoleRepository _roleRepository = roleRepository;
    private readonly IRolePermissionRepository _rolePermissionRepository = rolePermissionRepository;
    private readonly IPermissionRepository _permissionRepository = permissionRepository;
    private readonly IAuthzMapVersionRepository _versionRepository = versionRepository;
    private readonly IAuthzCacheInvalidator _cacheInvalidator = cacheInvalidator;
    private readonly IPostCommitActions _postCommitActions = postCommitActions;

    public async ValueTask<Result> Handle(SetRolePermissionsCommand command, CancellationToken cancellationToken)
    {
        var role = await _roleRepository.GetByKeyAsync(command.RoleKey, cancellationToken);

        if (role is null)
        {
            return Result.NotFound($"Role '{command.RoleKey}' not found.");
        }

        // Pass 1: resolve every requested key BEFORE staging any mutations.
        // Returning Invalid here leaves the DB untouched (no RemoveRange has been called yet).
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

        // Pass 2: diff-based update — compute adds and removes so unchanged rows are not re-created.
        // This avoids EF ordering surprises on the unique index when the old and new sets overlap,
        // and avoids bumping the version on a true no-op call.
        var desiredPermissionIds = newMappings.Select(m => m.PermissionId).ToHashSet();
        var existing = await _rolePermissionRepository.ListByRoleIdAsync(role.Id, cancellationToken);
        var existingPermissionIds = existing.Select(rp => rp.PermissionId).ToHashSet();

        var toRemove = existing.Where(rp => !desiredPermissionIds.Contains(rp.PermissionId)).ToList();
        var toAdd = newMappings.Where(m => !existingPermissionIds.Contains(m.PermissionId)).DistinctBy(m => m.PermissionId).ToList();

        if (toRemove.Count == 0 && toAdd.Count == 0)
        {
            return Result.Success(); // no-op: desired set equals existing set; don't churn rows or bump the version.
        }

        _rolePermissionRepository.RemoveRange(toRemove);
        foreach (var mapping in toAdd)
        {
            _rolePermissionRepository.Add(mapping);
        }

        (await _versionRepository.GetTrackedAsync(cancellationToken)).Bump();
        // Evict AFTER the transaction commits (TransactionBehavior drains this) — invalidating inline would
        // publish the eviction before the version bump + rows are durable, letting a concurrent read re-cache
        // the stale map until the TTL backstop.
        _postCommitActions.Enqueue(ct => _cacheInvalidator.InvalidateAsync(ct));
        return Result.Success();
    }
}
