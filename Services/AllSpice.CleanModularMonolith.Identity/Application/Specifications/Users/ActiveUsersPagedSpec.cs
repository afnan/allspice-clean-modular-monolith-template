using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.User;
using Ardalis.Specification;

namespace AllSpice.CleanModularMonolith.Identity.Application.Specifications.Users;

public sealed class ActiveUsersPagedSpec : Specification<User>
{
    public ActiveUsersPagedSpec(int page, int pageSize)
    {
        var skip = (Math.Max(1, page) - 1) * Math.Max(1, pageSize);

        Query
            .Where(u => u.IsActive)
            .OrderBy(u => u.Username)
            .Skip(skip)
            .Take(Math.Max(1, pageSize));
    }
}
