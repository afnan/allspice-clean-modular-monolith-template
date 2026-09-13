using AllSpice.CleanModularMonolith.Ledger.Domain.Aggregates;
using AllSpice.CleanModularMonolith.Ledger.Domain.Enums;
using AllSpice.CleanModularMonolith.Ledger.Domain.Events;
using AllSpice.CleanModularMonolith.Ledger.Domain.Exceptions;
using AllSpice.CleanModularMonolith.Ledger.Domain.ValueObjects;
using AllSpice.CleanModularMonolith.SharedKernel.Exceptions;

namespace AllSpice.CleanModularMonolith.Ledger.Domain.UnitTests.Accounts;

/// <summary>
/// Given events → When command → Then new events + state. The aggregate is pure: no store, no clock.
/// </summary>
public class AccountTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 13, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Owner = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static Account OpenAccount(Guid? id = null) => Account.Open(id ?? Guid.NewGuid(), Owner, Currency.Aud, Now);

    [Fact]
    public void Open_raises_AccountOpened_and_sets_initial_state()
    {
        var id = Guid.NewGuid();

        var account = Account.Open(id, Owner, Currency.Aud, Now);

        var opened = Assert.IsType<AccountOpened>(Assert.Single(account.UncommittedEvents));
        Assert.Equal(id, opened.AccountId);
        Assert.Equal(Owner, opened.OwnerUserId);
        Assert.Equal("AUD", opened.Currency);
        Assert.Equal(id, account.Id);
        Assert.Equal(Money.Zero(Currency.Aud), account.Balance);
        Assert.Equal(AccountStatus.Open, account.Status);
        Assert.Equal(Now, account.OpenedUtc);
        Assert.Equal(0, account.Version);
    }

    [Fact]
    public void Open_rejects_default_ids()
    {
        Assert.ThrowsAny<ArgumentException>(() => Account.Open(Guid.Empty, Owner, Currency.Aud, Now));
        Assert.ThrowsAny<ArgumentException>(() => Account.Open(Guid.NewGuid(), Guid.Empty, Currency.Aud, Now));
    }

    [Fact]
    public void Deposit_increases_balance_and_raises_FundsDeposited()
    {
        var account = OpenAccount();

        account.Deposit(Money.Of(150.25m, Currency.Aud), "INV-1", Now);

        Assert.Equal(150.25m, account.Balance.Amount);
        var deposited = Assert.IsType<FundsDeposited>(account.UncommittedEvents[^1]);
        Assert.Equal(150.25m, deposited.Amount);
        Assert.Equal("INV-1", deposited.Reference);
        Assert.Equal("AUD", deposited.Currency);
    }

    [Fact]
    public void Deposit_rejects_non_positive_amount_and_currency_mismatch()
    {
        var account = OpenAccount();

        Assert.Throws<BusinessRuleViolationException>(() => account.Deposit(Money.Zero(Currency.Aud), "x", Now));
        Assert.Throws<BusinessRuleViolationException>(() => account.Deposit(Money.Of(10, Currency.Usd), "x", Now));
    }

    [Fact]
    public void Withdraw_reduces_balance()
    {
        var account = OpenAccount();
        account.Deposit(Money.Of(100, Currency.Aud), "d", Now);

        account.Withdraw(Money.Of(40, Currency.Aud), "w", Now);

        Assert.Equal(60m, account.Balance.Amount);
        Assert.IsType<FundsWithdrawn>(account.UncommittedEvents[^1]);
    }

    [Fact]
    public void Withdraw_more_than_balance_throws_InsufficientFunds_with_stable_code()
    {
        var account = OpenAccount();
        account.Deposit(Money.Of(10, Currency.Aud), "d", Now);

        var ex = Assert.Throws<InsufficientFundsException>(() => account.Withdraw(Money.Of(10.01m, Currency.Aud), "w", Now));

        Assert.Equal("insufficient_funds", ex.Code);
        Assert.Equal(10m, account.Balance.Amount); // no state change on failure
        Assert.Equal(2, account.UncommittedEvents.Count);
    }

    [Fact]
    public void Close_requires_zero_balance_then_marks_for_archive()
    {
        var account = OpenAccount();
        account.Deposit(Money.Of(5, Currency.Aud), "d", Now);

        Assert.Throws<BusinessRuleViolationException>(() => account.Close(Now));

        account.Withdraw(Money.Of(5, Currency.Aud), "w", Now);
        account.Close(Now.AddMinutes(1));

        Assert.Equal(AccountStatus.Closed, account.Status);
        Assert.Equal(Now.AddMinutes(1), account.ClosedUtc);
        Assert.True(account.IsMarkedForArchive);
        Assert.IsType<AccountClosed>(account.UncommittedEvents[^1]);
    }

    [Fact]
    public void Closed_account_rejects_further_movements()
    {
        var account = OpenAccount();
        account.Close(Now);

        Assert.Throws<BusinessRuleViolationException>(() => account.Deposit(Money.Of(1, Currency.Aud), "d", Now));
        Assert.Throws<BusinessRuleViolationException>(() => account.Withdraw(Money.Of(1, Currency.Aud), "w", Now));
    }

    [Fact]
    public void Replaying_the_same_events_through_Apply_yields_identical_state()
    {
        var id = Guid.NewGuid();
        var original = Account.Open(id, Owner, Currency.Aud, Now);
        original.Deposit(Money.Of(100, Currency.Aud), "d", Now);
        original.Withdraw(Money.Of(30, Currency.Aud), "w", Now);

        // Marten builds aggregates by calling the public Apply methods on a fresh instance — mimic that.
        var replayed = (Account)Activator.CreateInstance(typeof(Account), nonPublic: true)!;
        foreach (var e in original.UncommittedEvents)
        {
            switch (e)
            {
                case AccountOpened o: replayed.Apply(o); break;
                case FundsDeposited d: replayed.Apply(d); break;
                case FundsWithdrawn w: replayed.Apply(w); break;
            }
        }

        Assert.Equal(original.Id, replayed.Id);
        Assert.Equal(original.Balance, replayed.Balance);
        Assert.Equal(original.Status, replayed.Status);
        Assert.Empty(replayed.UncommittedEvents); // Apply never records — only Raise does
    }
}
