using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;
using Ardalis.Result;
using Mediator;

namespace AllSpice.CleanModularMonolith.Identity.Application.Features.Authorization.Commands.CreatePermission;

public sealed class CreatePermissionCommandHandler(
    IPermissionRepository permissionRepository,
    IAuthzMapVersionRepository versionRepository,
    IAuthzCacheInvalidator cacheInvalidator)
    : IRequestHandler<CreatePermissionCommand, Result>
{
    private readonly IPermissionRepository _permissionRepository = permissionRepository;
    private readonly IAuthzMapVersionRepository _versionRepository = versionRepository;
    private readonly IAuthzCacheInvalidator _cacheInvalidator = cacheInvalidator;

    public async ValueTask<Result> Handle(CreatePermissionCommand command, CancellationToken cancellationToken)
    {
        var permission = Permission.Create(command.Key, command.Description, isSystem: false);
        await _permissionRepository.AddAsync(permission, cancellationToken);
        (await _versionRepository.GetTrackedAsync(cancellationToken)).Bump();
        await _cacheInvalidator.InvalidateAsync(cancellationToken);
        return Result.Success();
    }
}
