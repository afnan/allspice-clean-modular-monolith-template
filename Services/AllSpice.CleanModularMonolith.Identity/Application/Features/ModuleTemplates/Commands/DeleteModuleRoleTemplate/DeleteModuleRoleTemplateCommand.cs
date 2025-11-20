using Ardalis.Result;
using AllSpice.CleanModularMonolith.Identity.Application.Specifications.ModuleTemplates;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.ModuleRoleTemplate;
using AllSpice.CleanModularMonolith.SharedKernel.Repositories;
using Mediator;

namespace AllSpice.CleanModularMonolith.Identity.Application.Features.ModuleTemplates.Commands.DeleteModuleRoleTemplate;

public sealed record DeleteModuleRoleTemplateCommand(string TemplateKey) : IRequest<Result>;

public sealed class DeleteModuleRoleTemplateCommandHandler : IRequestHandler<DeleteModuleRoleTemplateCommand, Result>
{
    private readonly IRepository<ModuleRoleTemplate> _repository;

    public DeleteModuleRoleTemplateCommandHandler(IRepository<ModuleRoleTemplate> repository)
    {
        _repository = repository;
    }

    public async ValueTask<Result> Handle(DeleteModuleRoleTemplateCommand request, CancellationToken cancellationToken)
    {
        var normalizedKey = request.TemplateKey.Trim().ToLowerInvariant();
        var spec = new ModuleRoleTemplateByKeySpec(normalizedKey);
        var template = await _repository.FirstOrDefaultAsync(spec, cancellationToken);

        if (template is null)
        {
            return Result.NotFound($"Template '{normalizedKey}' was not found.");
        }

        await _repository.DeleteAsync(template, cancellationToken);

        return Result.Success();
    }
}


