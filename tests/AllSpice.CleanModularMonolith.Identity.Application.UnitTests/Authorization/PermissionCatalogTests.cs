using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Authorization;

namespace AllSpice.CleanModularMonolith.Identity.Application.UnitTests.Authorization;

public sealed class PermissionCatalogTests
{
    private sealed class CmsManifest : IModulePermissionManifest
    {
        public string ModuleKey => "cms";
        public IReadOnlyCollection<PermissionDefinition> Permissions =>
        [
            new("cms.access", "Access CMS"),
            new("cms:articles.publish", "Publish articles"),
        ];
    }

    [Fact]
    public void Collect_includes_app_level_and_module_keys()
    {
        var all = PermissionCatalog.Collect([new CmsManifest()]);
        var keys = all.Select(d => d.Key).ToHashSet();
        Assert.Contains("authz.manage", keys);
        Assert.Contains("cms.access", keys);
        Assert.Contains("cms:articles.publish", keys);
    }

    [Fact]
    public void Collect_rejects_a_malformed_module_key()
    {
        var bad = new BadManifest();
        Assert.Throws<InvalidOperationException>(() => PermissionCatalog.Collect([bad]));
    }

    private sealed class BadManifest : IModulePermissionManifest
    {
        public string ModuleKey => "bad";
        public IReadOnlyCollection<PermissionDefinition> Permissions => [new("NOT VALID", "x")];
    }
}
