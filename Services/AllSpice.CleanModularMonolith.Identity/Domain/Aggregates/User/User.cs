using Ardalis.GuardClauses;
using AllSpice.CleanModularMonolith.Identity.Domain.ValueObjects;
using AllSpice.CleanModularMonolith.SharedKernel.Common;

namespace AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.User;

/// <summary>
/// Represents a locally synced user from the external identity provider (Keycloak).
/// Enables relational references to users from other modules.
/// </summary>
public sealed class User : AuditableAggregateRoot
{
    private User()
    {
        Username = string.Empty;
    }

    private User(ExternalUserId externalId, string username, string? email, string? firstName, string? lastName)
    {
        Id = Guid.NewGuid();
        ExternalId = externalId;
        Username = username;
        Email = email;
        FirstName = firstName;
        LastName = lastName;
        IsActive = true;
        LastSyncedUtc = DateTimeOffset.UtcNow;
    }

    public ExternalUserId ExternalId { get; private set; } = ExternalUserId.From(Guid.Empty.ToString());

    public string Username { get; private set; }

    public string? Email { get; private set; }

    public string? FirstName { get; private set; }

    public string? LastName { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset LastSyncedUtc { get; private set; }

    public string DisplayName =>
        !string.IsNullOrWhiteSpace(FirstName) || !string.IsNullOrWhiteSpace(LastName)
            ? $"{FirstName} {LastName}".Trim()
            : Username;

    public static User Create(ExternalUserId externalId, string username, string? email, string? firstName, string? lastName)
    {
        Guard.Against.Null(externalId);
        Guard.Against.NullOrWhiteSpace(username);

        return new User(externalId, username.Trim(), email?.Trim(), firstName?.Trim(), lastName?.Trim());
    }

    public static User CreateFromExternalSync(ExternalUserId externalId, string email, string displayName)
    {
        Guard.Against.Null(externalId);
        Guard.Against.NullOrWhiteSpace(email);

        var trimmedEmail = email.Trim();
        var trimmedDisplay = displayName?.Trim() ?? string.Empty;
        var nameParts = trimmedDisplay.Split(' ', 2);
        var firstName = nameParts.Length > 0 ? nameParts[0] : null;
        var lastName = nameParts.Length > 1 ? nameParts[1] : null;

        return new User(externalId, trimmedEmail, trimmedEmail, firstName, lastName);
    }

    public void UpdateName(string? firstName, string? lastName)
    {
        FirstName = firstName?.Trim();
        LastName = lastName?.Trim();
    }

    public void UpdateFromSync(string username, string? email, string? firstName, string? lastName, bool isActive)
    {
        Guard.Against.NullOrWhiteSpace(username);

        Username = username.Trim();
        Email = email?.Trim();
        FirstName = firstName?.Trim();
        LastName = lastName?.Trim();
        IsActive = isActive;
        LastSyncedUtc = DateTimeOffset.UtcNow;
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
