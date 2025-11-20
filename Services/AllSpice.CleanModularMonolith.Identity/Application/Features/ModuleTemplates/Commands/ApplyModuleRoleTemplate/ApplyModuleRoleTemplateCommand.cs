using System.Collections.Generic;
using System.Linq;
using Ardalis.Result;
using AllSpice.CleanModularMonolith.Identity.Application.DTOs;
using AllSpice.CleanModularMonolith.Identity.Application.Features.ModuleAssignments.Commands.AssignModuleRole;
using AllSpice.CleanModularMonolith.Identity.Application.Specifications.ModuleTemplates;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.ModuleRoleTemplate;
using AllSpice.CleanModularMonolith.SharedKernel.Repositories;
using Mediator;

namespace AllSpice.CleanModularMonolith.Identity.Application.Features.ModuleTemplates.Commands.ApplyModuleRoleTemplate;

public sealed record ApplyModuleRoleTemplateCommand(
    string TemplateKey,
    string UserId,
    string AssignedBy) : IRequest<Result<IReadOnlyCollection<ModuleRoleAssignmentDto>>>;

public sealed class ApplyModuleRoleTemplateCommandHandler : IRequestHandler<ApplyModuleRoleTemplateCommand, Result<IReadOnlyCollection<ModuleRoleAssignmentDto>>>
{
    private readonly IRepository<ModuleRoleTemplate> _repository;
    private readonly IMediator _mediator;

    public ApplyModuleRoleTemplateCommandHandler(
        IRepository<ModuleRoleTemplate> repository,
        IMediator mediator)
    {
        _repository = repository;
        _mediator = mediator;
    }

    public async ValueTask<Result<IReadOnlyCollection<ModuleRoleAssignmentDto>>> Handle(ApplyModuleRoleTemplateCommand request, CancellationToken cancellationToken)
    {
        var spec = new ModuleRoleTemplateByKeySpec(request.TemplateKey.Trim().ToLowerInvariant());
        var template = await _repository.FirstOrDefaultAsync(spec, cancellationToken);

        if (template is null)
        {
            return Result.NotFound($"Template '{request.TemplateKey}' was not found.");
        }

        if (!template.Roles.Any())
        {
            return Result.Invalid(new ValidationError(string.Empty, "Template has no module roles configured."));
        }

        var assignments = new List<ModuleRoleAssignmentDto>();

        foreach (var role in template.Roles)
        {
            var result = await _mediator.Send(
                new AssignModuleRoleCommand(request.UserId, role.ModuleKey, role.RoleKey, request.AssignedBy),
                cancellationToken);

            if (!result.IsSuccess)
            {
                var message = result.Errors.FirstOrDefault() ?? "Failed to assign module role from template.";
                return Result.Error(message);
            }

            assignments.Add(result.Value);
        }

        return Result.Success<IReadOnlyCollection<ModuleRoleAssignmentDto>>(assignments);
    }
}


