namespace AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;

/// <summary>Maps a permission key to its dynamic policy name and back.</summary>
public static class PermissionPolicy
{
    public const string Prefix = "perm:";
    public static string For(string permissionKey) => Prefix + permissionKey;
    public static bool TryGetKey(string policyName, out string key)
    {
        if (policyName.StartsWith(Prefix, StringComparison.Ordinal))
        {
            key = policyName[Prefix.Length..];
            return key.Length > 0;
        }
        key = string.Empty;
        return false;
    }
}
