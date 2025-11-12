namespace AllSpice.CleanModularMonolith.Identity.Api.Endpoints.ModuleAssignments;

/// <summary>
/// Client request payload for assigning a module role to a user.
/// </summary>
public sealed class AssignModuleRoleRequest
{
    /// <summary>External directory user identifier.</summary>
    public string UserId { get; set; } = string.Empty;
    /// <summary>Module key (e.g. HR, Finance).</summary>
    public string ModuleKey { get; set; } = string.Empty;
    /// <summary>Role key within the module.</summary>
    public string RoleKey { get; set; } = string.Empty;
    /// <summary>User identifier performing the assignment.</summary>
    public string AssignedBy { get; set; } = string.Empty;
}


