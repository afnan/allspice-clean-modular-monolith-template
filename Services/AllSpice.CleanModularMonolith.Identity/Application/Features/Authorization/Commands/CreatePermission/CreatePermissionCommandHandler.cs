using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;
using AllSpice.CleanModularMonolith.SharedKernel.Behaviors;
using Ardalis.Result;
using Mediator;

namespace AllSpice.CleanModularMonolith.Identity.Application.Features.Authorization.Commands.CreatePermission;

public sealed class CreatePermissionCommandHandler(
    IPermissionRepository permissionRepository,
    IAuthzMapVersionRepository versionRepository,
    IAuthzCacheInvalidator cacheInvalidator,
    IPostCommitActions postCommitActions)
    : IRequestHandler<CreatePermissionCommand, Result>
{
    private readonly IPermissionRepository _permissionRepository = permissionRepository;
    private readonly IAuthzMapVersionRepository _versionRepository = versionRepository;
    private readonly IAuthzCacheInvalidator _cacheInvalidator = cacheInvalidator;
    private readonly IPostCommitActions _postCommitActions = postCommitActions;

    public async ValueTask<Result> Handle(CreatePermissionCommand command, CancellationToken cancellationToken)
    {
        var existing = await _permissionRepository.GetByKeyAsync(command.Key, cancellationToken);
        if (existing is not null)
        {
            return Result.Conflict($"Permission key '{command.Key}' already exists.");
        }

        var permission = Permission.Create(command.Key, command.Description, isSystem: false);
        await _permissionRepository.AddAsync(permission, cancellationToken);
        (await _versionRepository.GetTrackedAsync(cancellationToken)).Bump();
        // Evict AFTER commit (drained by TransactionBehavior) — see SetRolePermissionsCommandHandler.
        _postCommitActions.Enqueue(ct => _cacheInvalidator.InvalidateAsync(ct));
        return Result.Success();
    }
}
