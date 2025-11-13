namespace AllSpice.CleanModularMonolith.Identity.Api.Endpoints.ModuleTemplates.Apply;

public sealed class ApplyModuleRoleTemplateRequest
{
    /// <summary>
    /// Target user that will receive roles from the template.
    /// </summary>
    public string UserId { get; set; } = string.Empty;
}


