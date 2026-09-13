namespace AllSpice.CleanModularMonolith.SharedKernel.Exceptions;

/// <summary>
/// Thrown when a write lost an optimistic-concurrency race: the aggregate (or event stream) was modified by
/// another request between load and commit. Raised by the event-store participant (Marten
/// <c>ConcurrencyException</c>) and by <c>TransactionBehavior</c> for EF Core's
/// <c>DbUpdateConcurrencyException</c>, so both persistence styles surface the same 409 with the same
/// machine-readable <see cref="Code"/>. Clients should reload and retry.
/// </summary>
public sealed class ConcurrencyConflictException : DomainException
{
    public ConcurrencyConflictException(string message)
        : base(message)
    {
    }

    public ConcurrencyConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public override string Code => "concurrency_conflict";
}
