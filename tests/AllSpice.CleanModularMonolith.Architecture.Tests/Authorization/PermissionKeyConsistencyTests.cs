using System.Reflection;
using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Authorization;

namespace AllSpice.CleanModularMonolith.Architecture.Tests.Authorization;

/// <summary>
/// Arch-fitness tests (ADR-0007) pinning permission-key consistency across all modules.
/// <para>
/// Rule 1 — Every key in the collected catalog is well-formed (matches the domain regex) and
/// the catalog has no duplicates (PermissionCatalog.Collect deduplicates by key, so the
/// post-Collect count == distinct count trivially; the meaningful assertion is validity).
/// </para>
/// <para>
/// Rule 2 — Every key referenced via <see cref="HasPermissionAttribute"/> on an endpoint type
/// must be declared in the collected catalog.  FastEndpoints that call
/// <c>Policies(PermissionPolicy.For(...))</c> in <c>Configure()</c> are covered by convention:
/// each module's keys must appear in its <see cref="IModulePermissionManifest"/> — documented
/// as a golden rule in AGENTS.md.
/// </para>
/// <para>
/// Rule 3 — The Notifications module exposes a discoverable manifest, proving the multi-module
/// path works end-to-end.
/// </para>
/// </summary>
public sealed class PermissionKeyConsistencyTests
{
    /// <summary>Module assemblies scanned for manifests and endpoint types.</summary>
    private static readonly Assembly[] ModuleAssemblies =
    [
        typeof(AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.User.User).Assembly,
        typeof(AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates.Notification).Assembly,
    ];

    [Fact]
    public void All_catalog_keys_are_valid_and_unique()
    {
        var manifests = DiscoverManifests();
        var catalog = PermissionCatalog.Collect(manifests);

        // Every key must satisfy the domain pattern.
        Assert.All(catalog, d => Assert.True(PermissionKey.IsValid(d.Key), $"Invalid key '{d.Key}'"));

        // Post-Collect the dictionary already deduplicates; this confirms no residual duplicates.
        Assert.Equal(catalog.Select(d => d.Key).Distinct(StringComparer.Ordinal).Count(), catalog.Count);
    }

    [Fact]
    public void Every_HasPermission_attribute_key_is_declared()
    {
        var declared = PermissionCatalog.Collect(DiscoverManifests()).Select(d => d.Key).ToHashSet(StringComparer.Ordinal);

        foreach (var (type, key) in EndpointPermissionKeys())
        {
            Assert.True(declared.Contains(key),
                $"{type.Name} requires undeclared permission '{key}'");
        }
    }

    /// <summary>
    /// Asserts that the Notifications module registers a discoverable
    /// <see cref="IModulePermissionManifest"/> with module key "notifications".
    /// This is the primary RED/GREEN gate for Task 9.
    /// </summary>
    [Fact]
    public void Notifications_manifest_is_discoverable()
    {
        var manifests = DiscoverManifests();
        Assert.Contains(manifests, m => m.ModuleKey == "notifications");
    }

    // ── helpers ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Reflects over all module assemblies and instantiates every concrete
    /// <see cref="IModulePermissionManifest"/> that has a public parameterless constructor.
    /// </summary>
    private static IReadOnlyList<IModulePermissionManifest> DiscoverManifests()
    {
        var interfaceType = typeof(IModulePermissionManifest);

        return ModuleAssemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => !t.IsAbstract && !t.IsInterface && interfaceType.IsAssignableFrom(t))
            .Where(t => t.GetConstructor(Type.EmptyTypes) is not null)
            .Select(t => (IModulePermissionManifest)Activator.CreateInstance(t)!)
            .ToList();
    }

    /// <summary>
    /// Reflects over all endpoint types in the module assemblies and returns each
    /// <see cref="HasPermissionAttribute"/>-decorated type paired with its permission key.
    /// FastEndpoints that gate via <c>Policies(PermissionPolicy.For(...))</c> in
    /// <c>Configure()</c> are NOT captured here — their keys must appear in the module manifest.
    /// </summary>
    private static IEnumerable<(Type Type, string Key)> EndpointPermissionKeys()
    {
        return ModuleAssemblies
            .SelectMany(a => a.GetTypes())
            .SelectMany(t => t.GetCustomAttributes<HasPermissionAttribute>()
                .Where(attr => attr.Policy is not null && PermissionPolicy.TryGetKey(attr.Policy, out _))
                .Select(attr =>
                {
                    PermissionPolicy.TryGetKey(attr.Policy!, out var key);
                    return (t, key);
                }));
    }
}
