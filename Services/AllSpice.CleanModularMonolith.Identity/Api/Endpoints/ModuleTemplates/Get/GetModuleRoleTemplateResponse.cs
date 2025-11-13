using AllSpice.CleanModularMonolith.Identity.Application.DTOs;

namespace AllSpice.CleanModularMonolith.Identity.Api.Endpoints.ModuleTemplates.Get;

public sealed record GetModuleRoleTemplateResponse(
    Guid TemplateId,
    string TemplateKey,
    string Name,
    string Description,
    IReadOnlyCollection<GetModuleRoleTemplateRoleResponse> Roles)
{
    public static GetModuleRoleTemplateResponse FromDto(ModuleRoleTemplateDto dto) =>
        new(
            dto.TemplateId,
            dto.TemplateKey,
            dto.Name,
            dto.Description,
            dto.Roles.Select(role => new GetModuleRoleTemplateRoleResponse(role.ModuleKey, role.RoleKey)).ToList());
}

public sealed record GetModuleRoleTemplateRoleResponse(string ModuleKey, string RoleKey);


