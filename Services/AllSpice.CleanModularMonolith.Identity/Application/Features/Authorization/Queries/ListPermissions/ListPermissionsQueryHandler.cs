using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;
using Ardalis.Result;
using Mediator;

namespace AllSpice.CleanModularMonolith.Identity.Application.Features.Authorization.Queries.ListPermissions;

public sealed class ListPermissionsQueryHandler(IPermissionRepository permissionRepository)
    : IRequestHandler<ListPermissionsQuery, Result<IReadOnlyList<PermissionDto>>>
{
    private readonly IPermissionRepository _permissionRepository = permissionRepository;

    public async ValueTask<Result<IReadOnlyList<PermissionDto>>> Handle(
        ListPermissionsQuery request, CancellationToken cancellationToken)
    {
        var permissions = await _permissionRepository.ListAsync(cancellationToken);

        IReadOnlyList<PermissionDto> dtos = permissions
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => new PermissionDto(p.Id, p.Key, p.Description, p.IsSystem))
            .ToList();

        return Result<IReadOnlyList<PermissionDto>>.Success(dtos);
    }
}
