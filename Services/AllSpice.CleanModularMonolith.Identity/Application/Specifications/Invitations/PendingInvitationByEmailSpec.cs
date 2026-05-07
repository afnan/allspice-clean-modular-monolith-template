using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Invitation;
using AllSpice.CleanModularMonolith.Identity.Domain.Enums;
using Ardalis.Specification;

namespace AllSpice.CleanModularMonolith.Identity.Application.Specifications.Invitations;

public sealed class PendingInvitationByEmailSpec : Specification<Invitation>, ISingleResultSpecification<Invitation>
{
    public PendingInvitationByEmailSpec(string email)
    {
        var normalized = email.ToLowerInvariant();
        Query.Where(i => i.Email == normalized && i.Status == InvitationStatus.Pending);
    }
}
