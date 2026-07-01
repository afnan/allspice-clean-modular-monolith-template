using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.SharedKernel.Behaviors;
using Ardalis.Result;
using Mediator;

namespace AllSpice.CleanModularMonolith.Identity.Application.Features.Authorization.Commands.DeletePermission;

public sealed class DeletePermissionCommandHandler(
    IPermissionRepository permissionRepository,
    IAuthzMapVersionRepository versionRepository,
    IAuthzCacheInvalidator cacheInvalidator,
    IPostCommitActions postCommitActions)
    : IRequestHandler<DeletePermissionCommand, Result>
{
    private readonly IPermissionRepository _permissionRepository = permissionRepository;
    private readonly IAuthzMapVersionRepository _versionRepository = versionRepository;
    private readonly IAuthzCacheInvalidator _cacheInvalidator = cacheInvalidator;
    private readonly IPostCommitActions _postCommitActions = postCommitActions;

    public async ValueTask<Result> Handle(DeletePermissionCommand command, CancellationToken cancellationToken)
    {
        var permission = await _permissionRepository.GetByIdAsync<Guid>(command.Id, cancellationToken);

        if (permission is null)
        {
            return Result.NotFound();
        }

        if (permission.IsSystem)
        {
            return Result.Forbidden(); // code-referenced keys are deletion-protected (ADR-0008)
        }

        await _permissionRepository.DeleteAsync(permission, cancellationToken);
        (await _versionRepository.GetTrackedAsync(cancellationToken)).Bump();
        // Evict AFTER commit (drained by TransactionBehavior) — see SetRolePermissionsCommandHandler.
        _postCommitActions.Enqueue(ct => _cacheInvalidator.InvalidateAsync(ct));
        return Result.Success();
    }
}
