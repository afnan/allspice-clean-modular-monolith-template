using AllSpice.CleanModularMonolith.SharedKernel.Events;

namespace AllSpice.CleanModularMonolith.Ledger.Domain.Events;

/// <summary>Money debited from the account. Stored name: <c>funds_withdrawn</c>.</summary>
public sealed record FundsWithdrawn(Guid AccountId, decimal Amount, string Currency, string Reference, DateTimeOffset OccurredOnUtc) : IDomainEvent;
