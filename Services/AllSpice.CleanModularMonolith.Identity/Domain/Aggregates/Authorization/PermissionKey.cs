using System.Text.RegularExpressions;

namespace AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;

/// <summary>Domain rule for a well-formed permission key: lowercase, dot/colon-namespaced segments of
/// [a-z0-9-] — e.g. "authz.read", "cms.access", "cms:articles.publish". Owned by the Domain (the aggregate
/// invariant), not the Abstractions contract library.</summary>
public static partial class PermissionKey
{
    public static bool IsValid(string key)
        => !string.IsNullOrWhiteSpace(key) && KeyPattern().IsMatch(key);

    [GeneratedRegex(@"^[a-z0-9-]+(:[a-z0-9-]+(\.[a-z0-9-]+)*|(\.[a-z0-9-]+)+)$")]
    private static partial Regex KeyPattern();
}
