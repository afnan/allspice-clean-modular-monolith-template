namespace AllSpice.CleanModularMonolith.SharedKernel.Identity;

/// <summary>
/// Per-request holder for the resolved canonical local user UUID. The current user is fixed for the
/// duration of a request, so the host resolves the external subject to a local id <em>once</em> (at the
/// request boundary) and caches it here. The audit interceptor — via <see cref="ICurrentUserProvider"/>
/// — then stamps the local id rather than the external IdP subject. Registered as <c>Scoped</c>.
/// </summary>
public interface ICurrentUserContext
{
    /// <summary>The resolved local user id, or <c>null</c> when there is no authenticated/mirrored user.</summary>
    Guid? LocalUserId { get; }

    /// <summary>Records the local user id resolved for the current request. Called at most once per request.</summary>
    void Resolve(Guid? localUserId);
}

/// <summary>Default scoped <see cref="ICurrentUserContext"/> backed by a single per-request field.</summary>
public sealed class CurrentUserContext : ICurrentUserContext
{
    public Guid? LocalUserId { get; private set; }

    public void Resolve(Guid? localUserId) => LocalUserId = localUserId;
}
