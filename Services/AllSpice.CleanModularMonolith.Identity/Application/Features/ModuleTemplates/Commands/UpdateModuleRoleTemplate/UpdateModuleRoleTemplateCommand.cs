using Ardalis.Result;
using AllSpice.CleanModularMonolith.Identity.Application.DTOs;
using AllSpice.CleanModularMonolith.Identity.Application.Specifications.ModuleTemplates;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.ModuleRoleTemplate;
using AllSpice.CleanModularMonolith.Identity.Domain.ValueObjects;
using AllSpice.CleanModularMonolith.SharedKernel.Repositories;
using Mediator;

namespace AllSpice.CleanModularMonolith.Identity.Application.Features.ModuleTemplates.Commands.UpdateModuleRoleTemplate;

public sealed record UpdateModuleRoleTemplateCommand(
    string TemplateKey,
    string Name,
    string? Description,
    IReadOnlyCollection<ModuleRoleTemplateRoleDto> Roles) : IRequest<Result<ModuleRoleTemplateDto>>;

public sealed class UpdateModuleRoleTemplateCommandHandler : IRequestHandler<UpdateModuleRoleTemplateCommand, Result<ModuleRoleTemplateDto>>
{
    private readonly IRepository<ModuleRoleTemplate> _repository;

    public UpdateModuleRoleTemplateCommandHandler(IRepository<ModuleRoleTemplate> repository)
    {
        _repository = repository;
    }

    public async ValueTask<Result<ModuleRoleTemplateDto>> Handle(UpdateModuleRoleTemplateCommand request, CancellationToken cancellationToken)
    {
        var normalizedKey = request.TemplateKey.Trim().ToLowerInvariant();
        var spec = new ModuleRoleTemplateByKeySpec(normalizedKey);
        var template = await _repository.FirstOrDefaultAsync(spec, cancellationToken);

        if (template is null)
        {
            return Result.NotFound($"Template '{normalizedKey}' was not found.");
        }

        template.UpdateDetails(request.Name, request.Description);
        var roles = (request.Roles ?? Array.Empty<ModuleRoleTemplateRoleDto>())
            .Select(role => ModuleRoleTemplateRole.Create(role.ModuleKey, role.RoleKey));

        template.ReplaceRoles(roles);

        await _repository.UpdateAsync(template, cancellationToken);

        return Result.Success(ModuleRoleTemplateDto.From(template));
    }
}


