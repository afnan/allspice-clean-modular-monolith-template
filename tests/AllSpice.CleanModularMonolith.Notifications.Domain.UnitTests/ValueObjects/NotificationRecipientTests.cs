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
}


