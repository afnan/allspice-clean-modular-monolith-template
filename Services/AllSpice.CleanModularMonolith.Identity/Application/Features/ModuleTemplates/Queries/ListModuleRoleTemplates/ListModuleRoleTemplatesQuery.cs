using Ardalis.Result;
using AllSpice.CleanModularMonolith.Identity.Application.DTOs;
using AllSpice.CleanModularMonolith.Identity.Application.Specifications.ModuleTemplates;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.ModuleRoleTemplate;
using AllSpice.CleanModularMonolith.SharedKernel.Repositories;
using Mediator;

namespace AllSpice.CleanModularMonolith.Identity.Application.Features.ModuleTemplates.Queries.ListModuleRoleTemplates;

public sealed record ListModuleRoleTemplatesQuery() : IRequest<Result<IReadOnlyCollection<ModuleRoleTemplateDto>>>;

public sealed class ListModuleRoleTemplatesQueryHandler : IRequestHandler<ListModuleRoleTemplatesQuery, Result<IReadOnlyCollection<ModuleRoleTemplateDto>>>
{
    private readonly IReadRepository<ModuleRoleTemplate> _repository;

    public ListModuleRoleTemplatesQueryHandler(IReadRepository<ModuleRoleTemplate> repository)
    {
        _repository = repository;
    }

    public async ValueTask<Result<IReadOnlyCollection<ModuleRoleTemplateDto>>> Handle(ListModuleRoleTemplatesQuery request, CancellationToken cancellationToken)
    {
        var spec = new ModuleRoleTemplateListSpec();
        var templates = await _repository.ListAsync(spec, cancellationToken);
        return Result.Success(ModuleRoleTemplateDto.FromList(templates));
    }
}


