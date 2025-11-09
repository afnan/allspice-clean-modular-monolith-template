using Ardalis.Result;
using AllSpice.CleanModularMonolith.Identity.Application.DTOs;
using Mediator;

namespace AllSpice.CleanModularMonolith.Identity.Application.Features.ModuleAssignments.Commands.AssignModuleRole;

public sealed record AssignModuleRoleCommand(
    string UserId,
    string ModuleKey,
    string RoleKey,
    string AssignedBy) : IRequest<Result<ModuleRoleAssignmentDto>>;


