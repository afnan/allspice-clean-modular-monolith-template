using Ardalis.SmartEnum;

namespace AllSpice.CleanModularMonolith.Notifications.Domain.Enums;

public sealed class NotificationStatus : SmartEnum<NotificationStatus>
{
    public static readonly NotificationStatus Pending = new(nameof(Pending), 1);
    public static readonly NotificationStatus Dispatched = new(nameof(Dispatched), 2);
    public static readonly NotificationStatus Delivered = new(nameof(Delivered), 3);
    public static readonly NotificationStatus Failed = new(nameof(Failed), 4);
    public static readonly NotificationStatus Cancelled = new(nameof(Cancelled), 5);

    private NotificationStatus(string name, int value) : base(name, value)
    {
    }
}


