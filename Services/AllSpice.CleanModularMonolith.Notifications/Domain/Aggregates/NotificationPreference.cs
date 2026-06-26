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
public sealed class NotificationPreference : Entity, IAggregateRoot
{
    private NotificationPreference()
    {
    }

    private NotificationPreference(Guid userId, NotificationChannel channel, bool isEnabled, DateTimeOffset nowUtc)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Channel = channel;
        IsEnabled = isEnabled;
        CreatedUtc = nowUtc;
        UpdatedUtc = nowUtc;
    }

    /// <summary>Local user UUID (User.Id). Never the Keycloak external ID.</summary>
    public Guid UserId { get; private set; }

    public NotificationChannel Channel { get; private set; } = NotificationChannel.Email;

    public bool IsEnabled { get; private set; } = true;

    public DateTimeOffset CreatedUtc { get; private set; }

    public DateTimeOffset UpdatedUtc { get; private set; }

    public static NotificationPreference Create(Guid userId, NotificationChannel channel, bool isEnabled, DateTimeOffset nowUtc)
    {
        Guard.Against.Default(userId, nameof(userId));
        Guard.Against.Null(channel, nameof(channel));

        return new NotificationPreference(userId, channel, isEnabled, nowUtc);
    }

    public void Update(bool isEnabled, DateTimeOffset nowUtc)
    {
        IsEnabled = isEnabled;
        UpdatedUtc = nowUtc;
    }
}
