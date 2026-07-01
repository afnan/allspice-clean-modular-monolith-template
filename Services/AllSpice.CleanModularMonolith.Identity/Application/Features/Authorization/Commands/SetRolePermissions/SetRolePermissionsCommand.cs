using AllSpice.CleanModularMonolith.SharedKernel.Behaviors;
using Ardalis.Result;
using Mediator;

namespace AllSpice.CleanModularMonolith.Identity.Application.Features.Authorization.Commands.SetRolePermissions;

public sealed record SetRolePermissionsCommand(string RoleKey, IReadOnlyList<string> PermissionKeys)
    : IRequest<Result>, ITransactional;
