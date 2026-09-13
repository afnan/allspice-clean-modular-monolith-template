using AllSpice.CleanModularMonolith.Ledger.Domain.Enums;
using AllSpice.CleanModularMonolith.Ledger.Domain.Events;
using AllSpice.CleanModularMonolith.Ledger.Domain.Exceptions;
using AllSpice.CleanModularMonolith.Ledger.Domain.ValueObjects;
using AllSpice.CleanModularMonolith.SharedKernel.EventSourcing;
using AllSpice.CleanModularMonolith.SharedKernel.Events;
using AllSpice.CleanModularMonolith.SharedKernel.Exceptions;
using Ardalis.GuardClauses;

namespace AllSpice.CleanModularMonolith.Ledger.Domain.Aggregates;

/// <summary>
/// A money ledger account — the template's reference event-sourced aggregate (ADR-0009). It is a textbook
/// fit: every movement must be auditable, "balance as of" questions are natural, and the events themselves
/// are the business record. Command methods guard invariants and <c>Raise</c>; <c>Apply</c> methods are pure
/// state transitions used both here and by the store when it replays the stream.
/// </summary>
public sealed class Account : EventSourcedAggregate
{
    // Non-public parameterless ctor: used by the store to build the aggregate before replaying events.
    private Account()
    {
    }

    /// <summary>Local user UUID (User.Id) — never the Keycloak external id (ADR-0005).</summary>
    public Guid OwnerUserId { get; private set; }

    public Currency Currency { get; private set; } = Currency.Aud;

    public Money Balance { get; private set; } = Money.Zero(Currency.Aud);

    public AccountStatus Status { get; private set; } = AccountStatus.Open;

    public DateTimeOffset OpenedUtc { get; private set; }

    public DateTimeOffset? ClosedUtc { get; private set; }

    public static Account Open(Guid accountId, Guid ownerUserId, Currency currency, DateTimeOffset nowUtc)
    {
        Guard.Against.Default(accountId);
        Guard.Against.Default(ownerUserId);
        Guard.Against.Null(currency);

        var account = new Account();
        account.Raise(new AccountOpened(accountId, ownerUserId, currency.Code, nowUtc));
        return account;
    }

    public void Deposit(Money amount, string reference, DateTimeOffset nowUtc)
    {
        EnsureOpen();
        EnsureCurrency(amount);
        Guard.Against.NullOrWhiteSpace(reference);
        if (!amount.IsPositive)
        {
            throw new BusinessRuleViolationException("Deposit amount must be positive.");
        }

        Raise(new FundsDeposited(Id, amount.Amount, amount.Currency.Code, reference.Trim(), nowUtc));
    }

    public void Withdraw(Money amount, string reference, DateTimeOffset nowUtc)
    {
        EnsureOpen();
        EnsureCurrency(amount);
        Guard.Against.NullOrWhiteSpace(reference);
        if (!amount.IsPositive)
        {
            throw new BusinessRuleViolationException("Withdrawal amount must be positive.");
        }

        if (Balance.Amount < amount.Amount)
        {
            throw new InsufficientFundsException(Id, Balance.Amount, amount.Amount);
        }

        Raise(new FundsWithdrawn(Id, amount.Amount, amount.Currency.Code, reference.Trim(), nowUtc));
    }

    public void Close(DateTimeOffset nowUtc)
    {
        EnsureOpen();
        if (Balance.IsPositive)
        {
            throw new BusinessRuleViolationException("An account with a non-zero balance cannot be closed.");
        }

        Raise(new AccountClosed(Id, nowUtc));
        MarkForArchive(); // terminal: the stream is archived at the next save (never deleted)
    }

    // ---- State transitions (store replay convention). No validation here, ever. ----

    public void Apply(AccountOpened e)
    {
        Id = e.AccountId;
        OwnerUserId = e.OwnerUserId;
        Currency = Currency.FromCode(e.Currency);
        Balance = Money.Zero(Currency);
        Status = AccountStatus.Open;
        OpenedUtc = e.OccurredOnUtc;
    }

    public void Apply(FundsDeposited e) => Balance = Balance.Add(Money.Of(e.Amount, Currency));

    public void Apply(FundsWithdrawn e) => Balance = Balance.Subtract(Money.Of(e.Amount, Currency));

    public void Apply(AccountClosed e)
    {
        Status = AccountStatus.Closed;
        ClosedUtc = e.OccurredOnUtc;
    }

    protected override void When(IDomainEvent @event)
    {
        switch (@event)
        {
            case AccountOpened e: Apply(e); break;
            case FundsDeposited e: Apply(e); break;
            case FundsWithdrawn e: Apply(e); break;
            case AccountClosed e: Apply(e); break;
            default: throw new InvalidOperationException($"{nameof(Account)} cannot apply {@event.GetType().Name}.");
        }
    }

    private void EnsureOpen()
    {
        if (Status != AccountStatus.Open)
        {
            throw new BusinessRuleViolationException($"Account {Id} is {Status.Name.ToLowerInvariant()}.");
        }
    }

    private void EnsureCurrency(Money amount)
    {
        Guard.Against.Null(amount);
        if (amount.Currency != Currency)
        {
            throw new BusinessRuleViolationException($"Account {Id} is denominated in {Currency.Code}, not {amount.Currency.Code}.");
        }
    }
}
