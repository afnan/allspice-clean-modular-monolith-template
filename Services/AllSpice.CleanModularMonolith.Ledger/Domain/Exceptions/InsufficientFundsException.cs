using AllSpice.CleanModularMonolith.SharedKernel.Exceptions;

namespace AllSpice.CleanModularMonolith.Ledger.Domain.Exceptions;

/// <summary>A withdrawal exceeded the available balance. 422 with code <c>insufficient_funds</c>.</summary>
public sealed class InsufficientFundsException(Guid accountId, decimal balance, decimal requested)
    : BusinessRuleViolationException(
        $"Account {accountId} has a balance of {balance:0.00} but {requested:0.00} was requested.")
{
    public Guid AccountId { get; } = accountId;
    public decimal Balance { get; } = balance;
    public decimal Requested { get; } = requested;

    public override string Code => "insufficient_funds";
}
