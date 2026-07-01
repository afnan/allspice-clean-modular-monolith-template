using Ardalis.Result;
using Mediator;

namespace AllSpice.CleanModularMonolith.Identity.Application.Features.Authorization.Queries.ListRoles;

public sealed record ListRolesQuery : IRequest<Result<IReadOnlyList<RoleDto>>>;
