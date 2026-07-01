using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Authorization;

/// <summary>Combines app-level keys (Permissions.All) with every module manifest into the complete,
/// validated, de-duplicated code-referenced catalog.</summary>
public static class PermissionCatalog
{
    public static IReadOnlyCollection<PermissionDefinition> Collect(IEnumerable<IModulePermissionManifest> manifests)
    {
        var byKey = new Dictionary<string, PermissionDefinition>(StringComparer.Ordinal);

        foreach (var key in Permissions.All)
        {
            byKey[key] = new PermissionDefinition(key, $"System permission {key}");
        }

        foreach (var manifest in manifests)
        {
            foreach (var def in manifest.Permissions)
            {
                if (!PermissionKey.IsValid(def.Key))
                {
                    throw new InvalidOperationException(
                        $"Module '{manifest.ModuleKey}' declares an invalid permission key '{def.Key}'.");
                }

                byKey[def.Key] = def; // last definition wins; duplicates collapse
            }
        }

        return byKey.Values.ToList();
    }
}
