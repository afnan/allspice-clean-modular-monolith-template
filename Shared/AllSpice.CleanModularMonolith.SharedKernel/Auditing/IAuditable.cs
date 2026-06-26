namespace AllSpice.CleanModularMonolith.SharedKernel.Auditing;

/// <summary>
/// Read-only audit metadata. The columns are stamped centrally by <c>AuditableEntityInterceptor</c> on save
/// (via EF Core's change tracker), so there are deliberately no mutators here — audit is a pure persistence
/// concern and never leaks onto the domain's command surface.
/// </summary>
public interface IAuditable
{
    DateTimeOffset CreatedOnUtc { get; }
    string? CreatedBy { get; }
    DateTimeOffset? LastModifiedOnUtc { get; }
    string? LastModifiedBy { get; }
}


