using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;
using Ardalis.Result;
using Mediator;

namespace AllSpice.CleanModularMonolith.Identity.Application.Features.Authorization.Queries.ListRoles;

public sealed class ListRolesQueryHandler(IRoleRepository roleRepository)
    : IRequestHandler<ListRolesQuery, Result<IReadOnlyList<RoleDto>>>
{
    private readonly IRoleRepository _roleRepository = roleRepository;

    public async ValueTask<Result<IReadOnlyList<RoleDto>>> Handle(
        ListRolesQuery request, CancellationToken cancellationToken)
    {
        var roles = await _roleRepository.ListAsync(cancellationToken);

        IReadOnlyList<RoleDto> dtos = roles
            .OrderBy(r => r.Key, StringComparer.Ordinal)
            .Select(r => new RoleDto(r.Id, r.Key, r.Description))
            .ToList();

        return Result<IReadOnlyList<RoleDto>>.Success(dtos);
    }
}
