namespace AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;

/// <summary>The current request's resolved permission set. Scoped; computed lazily on first access from the
/// authenticated principal's role claims, so proxied routes that never check a permission pay nothing.</summary>
public interface ICurrentUserPermissions
{
    bool HasPermission(string permissionKey);
    IReadOnlySet<string> Permissions { get; }
}
