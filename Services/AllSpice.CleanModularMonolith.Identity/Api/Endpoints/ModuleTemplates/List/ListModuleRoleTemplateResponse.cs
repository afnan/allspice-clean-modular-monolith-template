using AllSpice.CleanModularMonolith.Identity.Application.DTOs;

namespace AllSpice.CleanModularMonolith.Identity.Api.Endpoints.ModuleTemplates.List;

public sealed record ListModuleRoleTemplateResponse(
    Guid TemplateId,
    string TemplateKey,
    string Name,
    string Description,
    IReadOnlyCollection<ListModuleRoleTemplateRoleResponse> Roles)
{
    public static ListModuleRoleTemplateResponse FromDto(ModuleRoleTemplateDto dto) =>
        new(
            dto.TemplateId,
            dto.TemplateKey,
            dto.Name,
            dto.Description,
            dto.Roles.Select(role => new ListModuleRoleTemplateRoleResponse(role.ModuleKey, role.RoleKey)).ToList());
}

public sealed record ListModuleRoleTemplateRoleResponse(string ModuleKey, string RoleKey);


