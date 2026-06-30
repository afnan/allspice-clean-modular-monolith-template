namespace AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;

/// <summary>
/// The closed set of permission keys referenced by code. Module-scoped keys use a
/// "<c>module:area.action</c>" namespace; the coarse module gate is "<c>module.access</c>".
/// Plan B's reconciler seeds these as <c>IsSystem</c>.
/// </summary>
public static class Permissions
{
    public const string AuthzRead = "authz.read";
    public const string AuthzManage = "authz.manage";

    /// <summary>All code-referenced keys. Modules contribute their own via the manifest (Plan B).</summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        AuthzRead,
        AuthzManage,
    };
}
