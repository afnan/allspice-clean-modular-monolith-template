using Ardalis.Specification;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.ModuleRoleTemplate;

namespace AllSpice.CleanModularMonolith.Identity.Application.Specifications.ModuleTemplates;

public sealed class ModuleRoleTemplateByKeySpec : Specification<ModuleRoleTemplate>, ISingleResultSpecification<ModuleRoleTemplate>
{
    public ModuleRoleTemplateByKeySpec(string templateKey)
    {
        Query
            .Where(template => template.TemplateKey == templateKey)
            .Include(template => template.Roles);
    }
}


