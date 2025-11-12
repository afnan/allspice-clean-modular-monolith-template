using Ardalis.Result;
using AllSpice.CleanModularMonolith.Identity.Application.DTOs;
using Mediator;

namespace AllSpice.CleanModularMonolith.Identity.Application.Features.ModuleAssignments.Commands.AssignModuleRole;

/// <summary>
/// Command representing a request to assign a module role to a user.
/// </summary>
/// <param name="UserId">External user identifier.</param>
/// <param name="ModuleKey">Module key.</param>
/// <param name="RoleKey">Role key within the module.</param>
/// <param name="AssignedBy">User performing the assignment.</param>
public sealed record AssignModuleRoleCommand(
    string UserId,
    string ModuleKey,
    string RoleKey,
    string AssignedBy) : IRequest<Result<ModuleRoleAssignmentDto>>;

