using AllSpice.CleanModularMonolith.SharedKernel.Events;

namespace AllSpice.CleanModularMonolith.Identity.Domain.Events;

public sealed class InvitationCreatedDomainEvent : DomainEventBase
{
    public Guid InvitationId { get; }
    public string Email { get; }
    public string FirstName { get; }
    public string LastName { get; }
    public string Role { get; }
    public Guid Token { get; }
    public string TempPassword { get; }
    public DateTimeOffset ExpiresAtUtc { get; }

    public InvitationCreatedDomainEvent(
        Guid invitationId,
        string email,
        string firstName,
        string lastName,
        string role,
        Guid token,
        string tempPassword,
        DateTimeOffset expiresAtUtc)
    {
        InvitationId = invitationId;
        Email = email;
        FirstName = firstName;
        LastName = lastName;
        Role = role;
        Token = token;
        TempPassword = tempPassword;
        ExpiresAtUtc = expiresAtUtc;
    }
}
