using Ardalis.Result;
using AllSpice.CleanModularMonolith.Identity.Application.DTOs;
using AllSpice.CleanModularMonolith.Identity.Application.Specifications.ModuleTemplates;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.ModuleRoleTemplate;
using AllSpice.CleanModularMonolith.Identity.Domain.ValueObjects;
using AllSpice.CleanModularMonolith.SharedKernel.Repositories;
using Mediator;

namespace AllSpice.CleanModularMonolith.Identity.Application.Features.ModuleTemplates.Commands.CreateModuleRoleTemplate;

public sealed record CreateModuleRoleTemplateCommand(
    string TemplateKey,
    string Name,
    string? Description,
    IReadOnlyCollection<ModuleRoleTemplateRoleDto> Roles) : IRequest<Result<ModuleRoleTemplateDto>>;

public sealed class CreateModuleRoleTemplateCommandHandler : IRequestHandler<CreateModuleRoleTemplateCommand, Result<ModuleRoleTemplateDto>>
{
    private readonly IRepository<ModuleRoleTemplate> _repository;

    public CreateModuleRoleTemplateCommandHandler(IRepository<ModuleRoleTemplate> repository)
    {
        _repository = repository;
    }

    public async ValueTask<Result<ModuleRoleTemplateDto>> Handle(CreateModuleRoleTemplateCommand request, CancellationToken cancellationToken)
    {
        var normalizedKey = request.TemplateKey.Trim().ToLowerInvariant();

        var existsSpec = new ModuleRoleTemplateByKeySpec(normalizedKey);
        if (await _repository.AnyAsync(existsSpec, cancellationToken))
        {
            return Result.Conflict($"A template with key '{normalizedKey}' already exists.");
        }

        var roles = (request.Roles ?? Array.Empty<ModuleRoleTemplateRoleDto>())
            .Select(role => ModuleRoleTemplateRole.Create(role.ModuleKey, role.RoleKey));
        var template = ModuleRoleTemplate.Create(normalizedKey, request.Name, request.Description, roles);

        var created = await _repository.AddAsync(template, cancellationToken);

        return Result.Success(ModuleRoleTemplateDto.From(created));
    }
}


