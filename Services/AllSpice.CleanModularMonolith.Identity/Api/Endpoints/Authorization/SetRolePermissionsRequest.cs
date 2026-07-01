namespace AllSpice.CleanModularMonolith.Identity.Api.Endpoints.Authorization;

public sealed class SetRolePermissionsRequest
{
    public IReadOnlyList<string> PermissionKeys { get; set; } = [];
}
