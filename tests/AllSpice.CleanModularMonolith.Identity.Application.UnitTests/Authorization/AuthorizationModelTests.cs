using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AllSpice.CleanModularMonolith.Identity.Application.UnitTests.Authorization;

public sealed class AuthorizationModelTests
{
    private static IdentityDbContext NewContext()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var options = new DbContextOptionsBuilder<IdentityDbContext>().UseSqlite(conn).Options;
        return new IdentityDbContext(options);
    }

    [Fact]
    public void Permission_key_has_a_unique_index()
    {
        using var ctx = NewContext();
        var entity = ctx.Model.FindEntityType(typeof(Permission))!;
        var keyProp = entity.FindProperty(nameof(Permission.Key))!;
        Assert.Contains(entity.GetIndexes(), i => i.IsUnique && i.Properties.Contains(keyProp));
    }

    [Fact]
    public void RolePermission_is_mapped()
        => Assert.NotNull(NewContext().Model.FindEntityType(typeof(RolePermission)));

    [Fact]
    public void RolePermission_composite_has_a_unique_index()
    {
        using var ctx = NewContext();
        var entity = ctx.Model.FindEntityType(typeof(RolePermission))!;
        var roleIdProp = entity.FindProperty(nameof(RolePermission.RoleId))!;
        var permissionIdProp = entity.FindProperty(nameof(RolePermission.PermissionId))!;
        Assert.Contains(entity.GetIndexes(), i =>
            i.IsUnique &&
            i.Properties.Contains(roleIdProp) &&
            i.Properties.Contains(permissionIdProp));
    }
}
