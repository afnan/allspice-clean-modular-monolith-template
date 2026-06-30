using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;
using Ardalis.Result;
using Mediator;

namespace AllSpice.CleanModularMonolith.Identity.Application.Features.Authorization.Queries.GetRolePermissions;

public sealed class GetRolePermissionsQueryHandler(
    IRoleRepository roleRepository,
    IRolePermissionRepository rolePermissionRepository,
    IPermissionRepository permissionRepository)
    : IRequestHandler<GetRolePermissionsQuery, Result<IReadOnlyList<string>>>
{
    private readonly IRoleRepository _roleRepository = roleRepository;
    private readonly IRolePermissionRepository _rolePermissionRepository = rolePermissionRepository;
    private readonly IPermissionRepository _permissionRepository = permissionRepository;

    public async ValueTask<Result<IReadOnlyList<string>>> Handle(
        GetRolePermissionsQuery request, CancellationToken cancellationToken)
    {
        var role = await _roleRepository.GetByKeyAsync(request.RoleKey, cancellationToken);

        if (role is null)
        {
            return Result<IReadOnlyList<string>>.NotFound($"Role '{request.RoleKey}' not found.");
        }

        var rolePermissions = await _rolePermissionRepository.ListByRoleIdAsync(role.Id, cancellationToken);

        if (rolePermissions.Count == 0)
        {
            return Result<IReadOnlyList<string>>.Success([]);
        }

        var permissionIds = rolePermissions.Select(rp => rp.PermissionId).ToHashSet();
        var allPermissions = await _permissionRepository.ListAsync(cancellationToken);

        IReadOnlyList<string> keys = allPermissions
            .Where(p => permissionIds.Contains(p.Id))
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => p.Key)
            .ToList();

        return Result<IReadOnlyList<string>>.Success(keys);
    }
}
