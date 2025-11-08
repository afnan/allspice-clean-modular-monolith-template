using Ardalis.GuardClauses;
using AllSpice.CleanModularMonolith.Notifications.Domain.Enums;
using AllSpice.CleanModularMonolith.SharedKernel.Common;

namespace AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;

public sealed class NotificationPreference : AggregateRoot
{
    private NotificationPreference()
    {
    }

    private NotificationPreference(string userId, NotificationChannel channel, bool isEnabled)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Channel = channel;
        IsEnabled = isEnabled;
        CreatedUtc = DateTimeOffset.UtcNow;
        UpdatedUtc = CreatedUtc;
    }

    public string UserId { get; private set; } = string.Empty;

    public NotificationChannel Channel { get; private set; } = NotificationChannel.Email;

    public bool IsEnabled { get; private set; } = true;

    public DateTimeOffset CreatedUtc { get; private set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedUtc { get; private set; } = DateTimeOffset.UtcNow;

    public static NotificationPreference Create(string userId, NotificationChannel channel, bool isEnabled)
    {
        Guard.Against.NullOrWhiteSpace(userId, nameof(userId));
        Guard.Against.Null(channel, nameof(channel));

        return new NotificationPreference(userId.Trim(), channel, isEnabled);
    }

    public void Update(bool isEnabled)
    {
        IsEnabled = isEnabled;
        UpdatedUtc = DateTimeOffset.UtcNow;
    }
}


