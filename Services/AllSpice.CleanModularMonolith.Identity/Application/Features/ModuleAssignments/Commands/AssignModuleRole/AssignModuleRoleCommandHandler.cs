using Ardalis.Result;
using AllSpice.CleanModularMonolith.Identity.Application.Contracts.External;
using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Identity.Application.DTOs;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.ModuleRoleAssignment;
using AllSpice.CleanModularMonolith.Identity.Domain.ValueObjects;
using Mediator;

namespace AllSpice.CleanModularMonolith.Identity.Application.Features.ModuleAssignments.Commands.AssignModuleRole;

/// <summary>
/// Handles assignment of module roles to Authentik users, ensuring directory validation and auditing.
/// </summary>
public sealed class AssignModuleRoleCommandHandler : IRequestHandler<AssignModuleRoleCommand, Result<ModuleRoleAssignmentDto>>
{
    private readonly IModuleDefinitionRepository _moduleDefinitionRepository;
    private readonly IModuleRoleAssignmentRepository _moduleRoleAssignmentRepository;
    private readonly IExternalDirectoryClient _externalDirectoryClient;

    public AssignModuleRoleCommandHandler(
        IModuleDefinitionRepository moduleDefinitionRepository,
        IModuleRoleAssignmentRepository moduleRoleAssignmentRepository,
        IExternalDirectoryClient externalDirectoryClient)
    {
        Guard.Against.Null(moduleDefinitionRepository);
        Guard.Against.Null(moduleRoleAssignmentRepository);
        Guard.Against.Null(externalDirectoryClient);

        _moduleDefinitionRepository = moduleDefinitionRepository;
        _moduleRoleAssignmentRepository = moduleRoleAssignmentRepository;
        _externalDirectoryClient = externalDirectoryClient;
    }

    /// <inheritdoc />
    public async ValueTask<Result<ModuleRoleAssignmentDto>> Handle(AssignModuleRoleCommand request, CancellationToken cancellationToken)
    {
        var module = await _moduleDefinitionRepository.GetByKeyAsync(request.ModuleKey, cancellationToken);
        if (module is null)
        {
            return Result.NotFound($"Module '{request.ModuleKey}' is not registered.");
        }

        var moduleRole = module.Roles.FirstOrDefault(role => role.RoleKey.Equals(request.RoleKey, StringComparison.OrdinalIgnoreCase));
        if (moduleRole is null)
        {
            return Result.NotFound($"Role '{request.RoleKey}' is not defined for module '{request.ModuleKey}'.");
        }

        var userId = ExternalUserId.From(request.UserId);

        if (!await _externalDirectoryClient.UserExistsAsync(request.UserId, cancellationToken))
        {
            return Result.NotFound($"User '{request.UserId}' does not exist in the external directory.");
        }

        var existingAssignment = await _moduleRoleAssignmentRepository.FindAssignmentAsync(userId, module.Key, moduleRole.RoleKey, cancellationToken);

        if (existingAssignment is not null && existingAssignment.IsActive())
        {
            return Result.Error($"User '{request.UserId}' already has role '{request.RoleKey}' for module '{request.ModuleKey}'.");
        }

        var assignment = ModuleRoleAssignment.Create(userId, module.Key, moduleRole.RoleKey, request.AssignedBy);

        await _moduleRoleAssignmentRepository.AddAsync(assignment, cancellationToken);

        var dto = new ModuleRoleAssignmentDto(
            assignment.Id,
            assignment.UserId.Value,
            assignment.ModuleKey,
            assignment.RoleKey,
            assignment.AssignedUtc,
            assignment.RevokedUtc);

        return Result.Success(dto);
    }
}


