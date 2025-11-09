namespace AllSpice.CleanModularMonolith.Identity.Api.Endpoints.ModuleAssignments;

public sealed class AssignModuleRoleRequest
{
    public string UserId { get; set; } = string.Empty;
    public string ModuleKey { get; set; } = string.Empty;
    public string RoleKey { get; set; } = string.Empty;
    public string AssignedBy { get; set; } = string.Empty;
}


