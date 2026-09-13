using AllSpice.CleanModularMonolith.SharedKernel.Events;

namespace AllSpice.CleanModularMonolith.Ledger.Domain.Events;

/// <summary>Money credited to the account. Stored name: <c>funds_deposited_v2</c> (v1 lacked <see cref="Reference"/>; see Legacy).</summary>
public sealed record FundsDeposited(Guid AccountId, decimal Amount, string Currency, string Reference, DateTimeOffset OccurredOnUtc) : IDomainEvent;
