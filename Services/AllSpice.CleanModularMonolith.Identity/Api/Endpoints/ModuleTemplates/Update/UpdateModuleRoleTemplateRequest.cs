namespace AllSpice.CleanModularMonolith.Identity.Api.Endpoints.ModuleTemplates.Update;

public sealed class UpdateModuleRoleTemplateRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<UpdateModuleRoleTemplateRoleRequest> Roles { get; set; } = new();
}

public sealed class UpdateModuleRoleTemplateRoleRequest
{
    public string ModuleKey { get; set; } = string.Empty;
    public string RoleKey { get; set; } = string.Empty;
}


