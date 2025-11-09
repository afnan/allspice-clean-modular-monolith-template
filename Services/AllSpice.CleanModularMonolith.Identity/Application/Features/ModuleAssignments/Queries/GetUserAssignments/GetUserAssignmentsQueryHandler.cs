using Ardalis.Result;
using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Identity.Application.DTOs;
using AllSpice.CleanModularMonolith.Identity.Domain.ValueObjects;
using Mediator;

namespace AllSpice.CleanModularMonolith.Identity.Application.Features.ModuleAssignments.Queries.GetUserAssignments;

public sealed class GetUserAssignmentsQueryHandler : IRequestHandler<GetUserAssignmentsQuery, Result<IReadOnlyCollection<ModuleRoleAssignmentDto>>>
{
    private readonly IModuleRoleAssignmentRepository _moduleRoleAssignmentRepository;

    public GetUserAssignmentsQueryHandler(IModuleRoleAssignmentRepository moduleRoleAssignmentRepository)
    {
        _moduleRoleAssignmentRepository = moduleRoleAssignmentRepository;
    }

    public async ValueTask<Result<IReadOnlyCollection<ModuleRoleAssignmentDto>>> Handle(GetUserAssignmentsQuery request, CancellationToken cancellationToken)
    {
        var userId = ExternalUserId.From(request.UserId);
        var assignments = await _moduleRoleAssignmentRepository.GetActiveAssignmentsAsync(userId, cancellationToken);

        var dtos = assignments
            .Select(assignment => new ModuleRoleAssignmentDto(
                assignment.Id,
                assignment.UserId.Value,
                assignment.ModuleKey,
                assignment.RoleKey,
                assignment.AssignedUtc,
                assignment.RevokedUtc))
            .ToList();

        return Result.Success<IReadOnlyCollection<ModuleRoleAssignmentDto>>(dtos);
    }
}


