using AllSpice.CleanModularMonolith.Identity.Application.DTOs;

namespace AllSpice.CleanModularMonolith.Identity.Api.Endpoints.ModuleTemplates.Create;

public sealed record CreateModuleRoleTemplateResponse(
    Guid TemplateId,
    string TemplateKey,
    string Name,
    string Description,
    IReadOnlyCollection<CreateModuleRoleTemplateRoleResponse> Roles)
{
    public static CreateModuleRoleTemplateResponse FromDto(ModuleRoleTemplateDto dto) =>
        new(
            dto.TemplateId,
            dto.TemplateKey,
            dto.Name,
            dto.Description,
            dto.Roles.Select(role => new CreateModuleRoleTemplateRoleResponse(role.ModuleKey, role.RoleKey)).ToList());
}

public sealed record CreateModuleRoleTemplateRoleResponse(string ModuleKey, string RoleKey);


