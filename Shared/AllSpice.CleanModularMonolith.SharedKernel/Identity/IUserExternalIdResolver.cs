namespace AllSpice.CleanModularMonolith.SharedKernel.Identity;

/// <summary>
/// Cross-module seam for translating between the canonical local user UUID and the external
/// identity-provider subject. The local UUID is the canonical identity used for audit stamping and
/// domain references; the external id is reserved for IdP/JWT boundaries.
/// </summary>
public interface IUserExternalIdResolver
{
    /// <summary>Resolves the external (IdP) subject for a local user, or <c>null</c> if unknown.</summary>
    Task<string?> GetExternalIdByLocalIdAsync(Guid localUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the canonical local user UUID for an external (IdP) subject, or <c>null</c> when the
    /// subject has not yet been mirrored locally (e.g. before the directory sync job has run).
    /// </summary>
    Task<Guid?> GetLocalIdByExternalIdAsync(string externalUserId, CancellationToken cancellationToken = default);
}
