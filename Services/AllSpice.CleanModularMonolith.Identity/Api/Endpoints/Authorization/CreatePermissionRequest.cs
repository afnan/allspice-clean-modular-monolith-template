namespace AllSpice.CleanModularMonolith.Identity.Api.Endpoints.Authorization;

public sealed class CreatePermissionRequest
{
    public string Key { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
