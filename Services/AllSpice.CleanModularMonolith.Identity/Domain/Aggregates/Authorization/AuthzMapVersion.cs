using AllSpice.CleanModularMonolith.SharedKernel.Common;

namespace AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Authorization;

/// <summary>Single-row monotonic version of the role→permission map; bumped on any mutation (Plan B)
/// to drive cache eviction. Seeded at version 0.</summary>
public sealed class AuthzMapVersion : Entity, IAggregateRoot
{
    public const string SingletonKey = "authz-map";

    private AuthzMapVersion() { }

    private AuthzMapVersion(long version) { Id = Guid.NewGuid(); Version = version; }

    public long Version { get; private set; }

    public static AuthzMapVersion Initial() => new(0);

    public void Bump() => Version++;
}
