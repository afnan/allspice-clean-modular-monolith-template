using AllSpice.CleanModularMonolith.Notifications.Domain.ValueObjects;

namespace AllSpice.CleanModularMonolith.Notifications.Domain.UnitTests.ValueObjects;

public class NotificationRecipientTests
{
    [Fact]
    public void Create_ReturnsRecipient_WhenContactProvided()
    {
        var recipient = NotificationRecipient.Create("user-123", "user@example.com", null);

        Assert.Equal("user-123", recipient.UserId);
        Assert.Equal("user@example.com", recipient.Email);
        Assert.Null(recipient.PhoneNumber);
    }

    [Fact]
    public void Create_Throws_WhenNoContactProvided()
    {
        Assert.Throws<ArgumentException>(() => NotificationRecipient.Create("user-123", null, null));
    }

    [Fact]
    public void Create_AllowsEmptyUserId_ForUserlessRecipient()
    {
        // An invitation email targets someone with no local account yet: no userId, identified by email.
        var recipient = NotificationRecipient.Create(string.Empty, "invitee@example.com", null);

        Assert.Equal(string.Empty, recipient.UserId);
        Assert.Equal("invitee@example.com", recipient.Email);
    }
}


