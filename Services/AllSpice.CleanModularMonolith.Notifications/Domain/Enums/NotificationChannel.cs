using Ardalis.SmartEnum;

namespace AllSpice.CleanModularMonolith.Notifications.Domain.Enums;

public sealed class NotificationChannel : SmartEnum<NotificationChannel>
{
    public static readonly NotificationChannel Email = new(nameof(Email), 1);
    public static readonly NotificationChannel Sms = new(nameof(Sms), 2);
    public static readonly NotificationChannel InApp = new(nameof(InApp), 3);

    private NotificationChannel(string name, int value) : base(name, value)
    {
    }
}


