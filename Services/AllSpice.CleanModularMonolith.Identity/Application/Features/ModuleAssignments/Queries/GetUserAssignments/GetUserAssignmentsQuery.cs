using Ardalis.Result;
using AllSpice.CleanModularMonolith.Identity.Application.DTOs;
using Mediator;

namespace AllSpice.CleanModularMonolith.Identity.Application.Features.ModuleAssignments.Queries.GetUserAssignments;

/// <summary>
/// Query that retrieves active module role assignments for a user.
/// </summary>
/// <param name="UserId">External user identifier.</param>
public sealed record GetUserAssignmentsQuery(string UserId) : IRequest<Result<IReadOnlyCollection<ModuleRoleAssignmentDto>>>;

