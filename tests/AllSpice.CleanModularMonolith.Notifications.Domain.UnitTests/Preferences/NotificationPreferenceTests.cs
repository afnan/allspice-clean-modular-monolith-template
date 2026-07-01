using AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Notifications.Domain.Enums;

namespace AllSpice.CleanModularMonolith.Notifications.Domain.UnitTests.Preferences;

public class NotificationPreferenceTests
{
    private static readonly Guid SampleUserId = new("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_InitializesFields()
    {
        var preference = NotificationPreference.Create(SampleUserId, NotificationChannel.Sms, true, Now);

        Assert.Equal(SampleUserId, preference.UserId);
        Assert.Equal(NotificationChannel.Sms, preference.Channel);
        Assert.True(preference.IsEnabled);
        Assert.Equal(Now, preference.CreatedUtc);
    }

    [Fact]
    public void Create_RejectsEmptyGuid()
    {
        Assert.Throws<ArgumentException>(() => NotificationPreference.Create(Guid.Empty, NotificationChannel.Email, true, Now));
    }

    [Fact]
    public void Update_SetsIsEnabledAndTimestamp()
    {
        var preference = NotificationPreference.Create(SampleUserId, NotificationChannel.Email, true, Now);

        var later = Now.AddMinutes(5);
        preference.Update(false, later);

        Assert.False(preference.IsEnabled);
        Assert.Equal(later, preference.UpdatedUtc);
    }
}
