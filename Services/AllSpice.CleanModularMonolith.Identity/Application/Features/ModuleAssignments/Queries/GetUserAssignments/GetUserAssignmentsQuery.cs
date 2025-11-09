using Ardalis.Result;
using AllSpice.CleanModularMonolith.Identity.Application.DTOs;
using Mediator;

namespace AllSpice.CleanModularMonolith.Identity.Application.Features.ModuleAssignments.Queries.GetUserAssignments;

public sealed record GetUserAssignmentsQuery(string UserId) : IRequest<Result<IReadOnlyCollection<ModuleRoleAssignmentDto>>>;


