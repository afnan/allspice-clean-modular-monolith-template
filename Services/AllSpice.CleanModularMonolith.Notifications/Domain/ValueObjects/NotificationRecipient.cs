using AllSpice.CleanModularMonolith.SharedKernel.ValueObjects;

namespace AllSpice.CleanModularMonolith.Notifications.Domain.ValueObjects;

public sealed class NotificationRecipient : ValueObject
{
    private NotificationRecipient()
    {
        UserId = string.Empty;
    }

    private NotificationRecipient(string userId, string? email, string? phoneNumber)
    {
        UserId = userId;
        Email = email;
        PhoneNumber = phoneNumber;
    }

    public string UserId { get; }

    public string? Email { get; }

    public string? PhoneNumber { get; }

    public static NotificationRecipient Create(string userId, string? email, string? phoneNumber)
    {
        // userId is optional: a notification can target a "userless" recipient identified only by a
        // contact method — e.g. an invitation email to someone who has no local account yet. When a
        // local user does exist, userId is their canonical local UUID. At least one contact method is
        // always required.
        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(phoneNumber))
        {
            throw new ArgumentException("At least one contact method (email or phone number) must be provided.", nameof(email));
        }

        return new NotificationRecipient(userId ?? string.Empty, email, phoneNumber);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return UserId;
        yield return Email;
        yield return PhoneNumber;
    }
}


