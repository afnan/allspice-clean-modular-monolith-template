using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.User;
using AllSpice.CleanModularMonolith.Identity.Domain.ValueObjects;

namespace AllSpice.CleanModularMonolith.Identity.Domain.UnitTests;

public class UserTests
{
    [Fact]
    public void Create_SetsAllProperties()
    {
        var externalId = ExternalUserId.From("kc-123");
        var user = User.Create(externalId, "jdoe", "j@test.com", "John", "Doe");

        Assert.Equal("kc-123", user.ExternalId.Value);
        Assert.Equal("jdoe", user.Username);
        Assert.Equal("j@test.com", user.Email);
        Assert.Equal("John", user.FirstName);
        Assert.Equal("Doe", user.LastName);
        Assert.True(user.IsActive);
        Assert.NotEqual(Guid.Empty, user.Id);
    }

    [Fact]
    public void CreateFromExternalSync_SetsEmailAsUsername()
    {
        var externalId = ExternalUserId.From("kc-456");
        var user = User.CreateFromExternalSync(externalId, "sync@test.com", "Jane Doe");

        Assert.Equal("sync@test.com", user.Username);
        Assert.Equal("sync@test.com", user.Email);
        Assert.Equal("Jane", user.FirstName);
        Assert.Equal("Doe", user.LastName);
    }

    [Fact]
    public void DisplayName_ReturnsFullName_WhenPresent()
    {
        var user = User.Create(ExternalUserId.From("kc-1"), "jdoe", null, "John", "Doe");
        Assert.Equal("John Doe", user.DisplayName);
    }

    [Fact]
    public void DisplayName_ReturnsUsername_WhenNoName()
    {
        var user = User.Create(ExternalUserId.From("kc-2"), "jdoe", null, null, null);
        Assert.Equal("jdoe", user.DisplayName);
    }

    [Fact]
    public void UpdateFromSync_UpdatesAllFields()
    {
        var user = User.Create(ExternalUserId.From("kc-3"), "old", "old@test.com", "Old", "Name");

        user.UpdateFromSync("newuser", "new@test.com", "New", "Name", false);

        Assert.Equal("newuser", user.Username);
        Assert.Equal("new@test.com", user.Email);
        Assert.Equal("New", user.FirstName);
        Assert.False(user.IsActive);
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var user = User.Create(ExternalUserId.From("kc-4"), "u", null, null, null);
        Assert.True(user.IsActive);

        user.Deactivate();
        Assert.False(user.IsActive);
    }

    [Fact]
    public void Activate_SetsIsActiveTrue()
    {
        var user = User.Create(ExternalUserId.From("kc-5"), "u", null, null, null);
        user.Deactivate();

        user.Activate();
        Assert.True(user.IsActive);
    }

    [Fact]
    public void Create_Throws_WhenUsernameNull()
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            User.Create(ExternalUserId.From("kc-6"), null!, null, null, null));
    }

    [Fact]
    public void Create_TrimsWhitespace()
    {
        var user = User.Create(ExternalUserId.From("kc-7"), " jdoe ", " j@test.com ", " John ", " Doe ");
        Assert.Equal("jdoe", user.Username);
        Assert.Equal("j@test.com", user.Email);
        Assert.Equal("John", user.FirstName);
        Assert.Equal("Doe", user.LastName);
    }
}
