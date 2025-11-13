using AllSpice.CleanModularMonolith.Identity.Application.DTOs;

namespace AllSpice.CleanModularMonolith.Identity.Api.Endpoints.ModuleTemplates.Update;

public sealed record UpdateModuleRoleTemplateResponse(
    Guid TemplateId,
    string TemplateKey,
    string Name,
    string Description,
    IReadOnlyCollection<UpdateModuleRoleTemplateRoleResponse> Roles)
{
    public static UpdateModuleRoleTemplateResponse FromDto(ModuleRoleTemplateDto dto) =>
        new(
            dto.TemplateId,
            dto.TemplateKey,
            dto.Name,
            dto.Description,
            dto.Roles.Select(role => new UpdateModuleRoleTemplateRoleResponse(role.ModuleKey, role.RoleKey)).ToList());
}

public sealed record UpdateModuleRoleTemplateRoleResponse(string ModuleKey, string RoleKey);


