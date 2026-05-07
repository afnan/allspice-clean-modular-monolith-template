using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.User;
using Ardalis.Specification;

namespace AllSpice.CleanModularMonolith.Identity.Application.Specifications.Users;

public sealed class UserByExternalIdSpec : Specification<User>, ISingleResultSpecification<User>
{
    public UserByExternalIdSpec(string externalId)
    {
        Query.Where(u => u.ExternalId.Value == externalId);
    }
}
