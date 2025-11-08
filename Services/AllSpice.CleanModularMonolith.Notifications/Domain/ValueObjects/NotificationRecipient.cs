using Ardalis.GuardClauses;
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
        Guard.Against.NullOrWhiteSpace(userId, nameof(userId));

        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(phoneNumber))
        {
            throw new ArgumentException("At least one contact method (email or phone number) must be provided.", nameof(email));
        }

        return new NotificationRecipient(userId, email, phoneNumber);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return UserId;
        yield return Email;
        yield return PhoneNumber;
    }
}


