using Ardalis.GuardClauses;
using AllSpice.CleanModularMonolith.Identity.Domain.Enums;
using AllSpice.CleanModularMonolith.Identity.Domain.Events;
using AllSpice.CleanModularMonolith.SharedKernel.Common;
using AllSpice.CleanModularMonolith.SharedKernel.Exceptions;

namespace AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Invitation;

public sealed class Invitation : AuditableAggregateRoot
{
    private Invitation()
    {
        Email = string.Empty;
        FirstName = string.Empty;
        LastName = string.Empty;
        Role = string.Empty;
        KeycloakUserId = string.Empty;
    }

    public string Email { get; private set; }
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public string Role { get; private set; }
    public Guid Token { get; private set; }
    public InvitationStatus Status { get; private set; } = InvitationStatus.Pending;
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public string? CreatedByUserId { get; private set; }
    public string KeycloakUserId { get; private set; }
    public Guid LocalUserId { get; private set; }

    public static Invitation Create(
        string email,
        string firstName,
        string lastName,
        string role,
        string keycloakUserId,
        Guid localUserId,
        string createdByUserId,
        string tempPassword)
    {
        Guard.Against.NullOrWhiteSpace(email);
        Guard.Against.NullOrWhiteSpace(firstName);
        Guard.Against.NullOrWhiteSpace(role);
        Guard.Against.NullOrWhiteSpace(keycloakUserId);
        Guard.Against.Default(localUserId);

        var invitation = new Invitation
        {
            Id = Guid.NewGuid(),
            Email = email.Trim().ToLowerInvariant(),
            FirstName = firstName.Trim(),
            LastName = (lastName ?? string.Empty).Trim(),
            Role = role,
            Token = Guid.NewGuid(),
            Status = InvitationStatus.Pending,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(48),
            CreatedByUserId = createdByUserId,
            KeycloakUserId = keycloakUserId,
            LocalUserId = localUserId
        };

        invitation.RegisterDomainEvent(new InvitationCreatedDomainEvent(
            invitation.Id,
            invitation.Email,
            invitation.FirstName,
            invitation.LastName,
            invitation.Role,
            invitation.Token,
            tempPassword,
            invitation.ExpiresAtUtc));

        return invitation;
    }

    public void MarkAccepted()
    {
        Guard.Against.InvalidInput(Status, nameof(Status),
            s => s == InvitationStatus.Pending,
            "Only pending invitations can be accepted.");

        if (IsExpired())
            throw new DomainValidationException("This invitation has expired.");

        Status = InvitationStatus.Accepted;
    }

    public void Resend()
    {
        Guard.Against.InvalidInput(Status, nameof(Status),
            s => s == InvitationStatus.Pending,
            "Only pending invitations can be resent.");

        ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(48);
    }

    public bool IsExpired() => DateTimeOffset.UtcNow > ExpiresAtUtc;
}
