namespace AllSpice.CleanModularMonolith.Identity.Application.DTOs;

using System.Collections.Generic;
using System.Linq;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.ModuleRoleTemplate;

public sealed record ModuleRoleTemplateDto(
    Guid TemplateId,
    string TemplateKey,
    string Name,
    string Description,
    IReadOnlyCollection<ModuleRoleTemplateRoleDto> Roles)
{
    public static ModuleRoleTemplateDto From(ModuleRoleTemplate template) =>
        new(
            template.Id,
            template.TemplateKey,
            template.Name,
            template.Description,
            template.Roles.Select(role => new ModuleRoleTemplateRoleDto(role.ModuleKey, role.RoleKey)).ToList());

    public static IReadOnlyCollection<ModuleRoleTemplateDto> FromList(IEnumerable<ModuleRoleTemplate> templates) =>
        templates.Select(From).ToList();
}

public sealed record ModuleRoleTemplateRoleDto(string ModuleKey, string RoleKey);


