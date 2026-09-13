namespace AllSpice.CleanModularMonolith.Ledger.Domain.Events.Legacy;

/// <summary>
/// The ORIGINAL shape of <c>FundsDeposited</c> (stored name <c>funds_deposited</c>), kept only so old rows can
/// still be deserialised and upcast. Never raised; not an <c>IDomainEvent</c>. This is the template's worked
/// example of event versioning — see ADR-0009 and AGENTS.md "Evolve an event".
/// </summary>
public sealed record FundsDepositedV1(Guid AccountId, decimal Amount, string Currency, DateTimeOffset OccurredOnUtc);
