using Ardalis.Result;
using AllSpice.CleanModularMonolith.Identity.Application.DTOs;
using AllSpice.CleanModularMonolith.Identity.Application.Specifications.ModuleTemplates;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.ModuleRoleTemplate;
using AllSpice.CleanModularMonolith.SharedKernel.Repositories;
using Mediator;

namespace AllSpice.CleanModularMonolith.Identity.Application.Features.ModuleTemplates.Queries.GetModuleRoleTemplate;

public sealed record GetModuleRoleTemplateQuery(string TemplateKey) : IRequest<Result<ModuleRoleTemplateDto>>;

public sealed class GetModuleRoleTemplateQueryHandler : IRequestHandler<GetModuleRoleTemplateQuery, Result<ModuleRoleTemplateDto>>
{
    private readonly IReadRepository<ModuleRoleTemplate> _repository;

    public GetModuleRoleTemplateQueryHandler(IReadRepository<ModuleRoleTemplate> repository)
    {
        _repository = repository;
    }

    public async ValueTask<Result<ModuleRoleTemplateDto>> Handle(GetModuleRoleTemplateQuery request, CancellationToken cancellationToken)
    {
        var spec = new ModuleRoleTemplateByKeySpec(request.TemplateKey.Trim().ToLowerInvariant());
        var template = await _repository.FirstOrDefaultAsync(spec, cancellationToken);

        if (template is null)
        {
            return Result.NotFound($"Template '{request.TemplateKey}' was not found.");
        }

        return Result.Success(ModuleRoleTemplateDto.From(template));
    }
}


