using AllSpice.CleanModularMonolith.SharedKernel.Events;

namespace AllSpice.CleanModularMonolith.Ledger.Domain.Events;

/// <summary>Terminal event; the stream is archived when it is saved. Stored name: <c>account_closed</c>.</summary>
public sealed record AccountClosed(Guid AccountId, DateTimeOffset OccurredOnUtc) : IDomainEvent;
