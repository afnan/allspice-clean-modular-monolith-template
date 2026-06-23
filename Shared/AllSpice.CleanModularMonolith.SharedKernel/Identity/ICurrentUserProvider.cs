namespace AllSpice.CleanModularMonolith.SharedKernel.Identity;

/// <summary>
/// Supplies the identifier of the user responsible for the current operation, used for audit
/// stamping (see <see cref="Auditing.IAuditable"/>). Implemented at the host boundary (e.g. from
/// the authenticated <c>ClaimsPrincipal</c>); resolves to <c>null</c> for background work with no
/// user context. Registered as a singleton so it is safe to inject into pooled-DbContext interceptors.
/// </summary>
public interface ICurrentUserProvider
{
    /// <summary>The current user's identifier, or <c>null</c> when there is no authenticated user.</summary>
    string? UserId { get; }
}

/// <summary>Default <see cref="ICurrentUserProvider"/> that always returns <c>null</c> (no user context).</summary>
public sealed class NullCurrentUserProvider : ICurrentUserProvider
{
    public string? UserId => null;
}
