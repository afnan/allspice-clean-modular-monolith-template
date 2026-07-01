using System;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;
using Xunit;

namespace AllSpice.CleanModularMonolith.Identity.Application.UnitTests.Authorization;

public sealed class AuthorizationEntitiesTests
{
    [Fact]
    public void Permission_Create_sets_fields_and_generates_id()
    {
        var p = Permission.Create("cms:articles.publish", "Publish articles", isSystem: true);
        Assert.NotEqual(Guid.Empty, p.Id);
        Assert.Equal("cms:articles.publish", p.Key);
        Assert.True(p.IsSystem);
    }

    [Fact]
    public void Permission_Create_rejects_malformed_key()
        => Assert.Throws<ArgumentException>(() => Permission.Create("Bad Key", "x", false));

    [Fact]
    public void Role_Create_normalizes_nothing_but_requires_key()
        => Assert.Throws<ArgumentException>(() => Role.Create("", null));

    [Fact]
    public void AuthzMapVersion_Bump_increments()
    {
        var v = AuthzMapVersion.Initial();
        var before = v.Version;
        v.Bump();
        Assert.Equal(before + 1, v.Version);
    }
}
