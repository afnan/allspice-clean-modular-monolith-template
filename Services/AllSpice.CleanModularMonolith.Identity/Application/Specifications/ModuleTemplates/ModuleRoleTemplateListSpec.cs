using Ardalis.Specification;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.ModuleRoleTemplate;

namespace AllSpice.CleanModularMonolith.Identity.Application.Specifications.ModuleTemplates;

public sealed class ModuleRoleTemplateListSpec : Specification<ModuleRoleTemplate>
{
    public ModuleRoleTemplateListSpec()
    {
        Query
            .Include(template => template.Roles)
            .OrderBy(template => template.Name);
    }
}


