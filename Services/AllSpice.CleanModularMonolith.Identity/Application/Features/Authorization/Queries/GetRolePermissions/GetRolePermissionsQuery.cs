using Ardalis.Result;
using Mediator;

namespace AllSpice.CleanModularMonolith.Identity.Application.Features.Authorization.Queries.GetRolePermissions;

public sealed record GetRolePermissionsQuery(string RoleKey) : IRequest<Result<IReadOnlyList<string>>>;
