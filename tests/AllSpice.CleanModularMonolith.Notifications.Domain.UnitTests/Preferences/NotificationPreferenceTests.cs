using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Notifications.Domain.Enums;

namespace AllSpice.CleanModularMonolith.Notifications.Domain.UnitTests.Preferences;

public class NotificationPreferenceTests
{
    [Fact]
    public void Create_InitializesFields()
    {
        var preference = NotificationPreference.Create(" user-123 ", NotificationChannel.Sms, true);

        Assert.Equal("user-123", preference.UserId);
        Assert.Equal(NotificationChannel.Sms, preference.Channel);
        Assert.True(preference.IsEnabled);
    }

    [Fact]
    public void Update_SetsIsEnabledAndTimestamp()
    {
        var preference = NotificationPreference.Create("user-123", NotificationChannel.Email, true);
        var originalUpdatedUtc = preference.UpdatedUtc;

        preference.Update(false);

        Assert.False(preference.IsEnabled);
        Assert.True(preference.UpdatedUtc > originalUpdatedUtc);
    }
}


