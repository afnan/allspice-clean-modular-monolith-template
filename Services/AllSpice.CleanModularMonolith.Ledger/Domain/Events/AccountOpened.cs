using AllSpice.CleanModularMonolith.SharedKernel.Events;

namespace AllSpice.CleanModularMonolith.Ledger.Domain.Events;

/// <summary>A ledger account was opened for a user. Stored name: <c>account_opened</c>.</summary>
public sealed record AccountOpened(Guid AccountId, Guid OwnerUserId, string Currency, DateTimeOffset OccurredOnUtc) : IDomainEvent;
