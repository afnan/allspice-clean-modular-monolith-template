namespace AllSpice.CleanModularMonolith.Identity.Api.Endpoints.ModuleTemplates.Create;

public sealed class CreateModuleRoleTemplateRequest
{
    public string TemplateKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<CreateModuleRoleTemplateRoleRequest> Roles { get; set; } = new();
}

public sealed class CreateModuleRoleTemplateRoleRequest
{
    public string ModuleKey { get; set; } = string.Empty;
    public string RoleKey { get; set; } = string.Empty;
}


