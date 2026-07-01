using Ardalis.Result;
using Mediator;

namespace AllSpice.CleanModularMonolith.Identity.Application.Features.Authorization.Queries.ListPermissions;

public sealed record ListPermissionsQuery : IRequest<Result<IReadOnlyList<PermissionDto>>>;
