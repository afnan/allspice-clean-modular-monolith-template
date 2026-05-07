using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Notifications.Domain.Enums;

namespace AllSpice.CleanModularMonolith.Notifications.Domain.UnitTests.Preferences;

public class NotificationPreferenceTests
{
    private static readonly Guid SampleUserId = new("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Create_InitializesFields()
    {
        var preference = NotificationPreference.Create(SampleUserId, NotificationChannel.Sms, true);

        Assert.Equal(SampleUserId, preference.UserId);
        Assert.Equal(NotificationChannel.Sms, preference.Channel);
        Assert.True(preference.IsEnabled);
    }

    [Fact]
    public void Create_RejectsEmptyGuid()
    {
        Assert.Throws<ArgumentException>(() => NotificationPreference.Create(Guid.Empty, NotificationChannel.Email, true));
    }

    [Fact]
    public void Update_SetsIsEnabledAndTimestamp()
    {
        var preference = NotificationPreference.Create(SampleUserId, NotificationChannel.Email, true);
        var originalUpdatedUtc = preference.UpdatedUtc;

        preference.Update(false);

        Assert.False(preference.IsEnabled);
        Assert.True(preference.UpdatedUtc > originalUpdatedUtc);
    }
}
