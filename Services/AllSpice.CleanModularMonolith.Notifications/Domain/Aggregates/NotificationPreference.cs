using Ardalis.GuardClauses;
using AllSpice.CleanModularMonolith.Notifications.Domain.Enums;
using AllSpice.CleanModularMonolith.SharedKernel.Common;

namespace AllSpice.CleanModularMonolith.Notifications.Domain.Aggregates;

/// <summary>
/// User-scoped opt-in/out preference for a single notification channel.
/// Keyed by the LOCAL user UUID (User.Id) — not the Keycloak external ID. The
/// canonical local identity convention is enforced here so a preference written
/// from one code path can be read from another without identity drift.
/// </summary>
public sealed class NotificationPreference : AggregateRoot
{
    private NotificationPreference()
    {
    }

    private NotificationPreference(Guid userId, NotificationChannel channel, bool isEnabled)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Channel = channel;
        IsEnabled = isEnabled;
        CreatedUtc = DateTimeOffset.UtcNow;
        UpdatedUtc = CreatedUtc;
    }

    /// <summary>Local user UUID (User.Id). Never the Keycloak external ID.</summary>
    public Guid UserId { get; private set; }

    public NotificationChannel Channel { get; private set; } = NotificationChannel.Email;

    public bool IsEnabled { get; private set; } = true;

    public DateTimeOffset CreatedUtc { get; private set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedUtc { get; private set; } = DateTimeOffset.UtcNow;

    public static NotificationPreference Create(Guid userId, NotificationChannel channel, bool isEnabled)
    {
        Guard.Against.Default(userId, nameof(userId));
        Guard.Against.Null(channel, nameof(channel));

        return new NotificationPreference(userId, channel, isEnabled);
    }

    public void Update(bool isEnabled)
    {
        IsEnabled = isEnabled;
        UpdatedUtc = DateTimeOffset.UtcNow;
    }
}
