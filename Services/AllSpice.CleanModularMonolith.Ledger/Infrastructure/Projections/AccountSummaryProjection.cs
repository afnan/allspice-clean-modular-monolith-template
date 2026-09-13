using AllSpice.CleanModularMonolith.Ledger.Domain.Enums;
using AllSpice.CleanModularMonolith.Ledger.Domain.Events;
using JasperFx.Events;
using Marten.Events.Aggregation;

namespace AllSpice.CleanModularMonolith.Ledger.Infrastructure.Projections;

/// <summary>
/// Builds <see cref="AccountSummary"/> from the stream. Uses <c>IEvent&lt;T&gt;</c> overloads where the stream
/// version is needed (requires <c>EventAppendMode.Rich</c>, set by <c>AddModuleEventStore</c>).
/// </summary>
public sealed class AccountSummaryProjection : SingleStreamProjection<AccountSummary, Guid>
{
    public static AccountSummary Create(IEvent<AccountOpened> e) => new()
    {
        Id = e.Data.AccountId,
        OwnerUserId = e.Data.OwnerUserId,
        Currency = e.Data.Currency,
        Balance = 0m,
        Status = AccountStatus.Open.Name,
        Version = e.Version,
        LastActivityUtc = e.Data.OccurredOnUtc,
    };

    public static void Apply(IEvent<FundsDeposited> e, AccountSummary summary)
    {
        summary.Balance += e.Data.Amount;
        summary.Version = e.Version;
        summary.LastActivityUtc = e.Data.OccurredOnUtc;
    }

    public static void Apply(IEvent<FundsWithdrawn> e, AccountSummary summary)
    {
        summary.Balance -= e.Data.Amount;
        summary.Version = e.Version;
        summary.LastActivityUtc = e.Data.OccurredOnUtc;
    }

    public static void Apply(IEvent<AccountClosed> e, AccountSummary summary)
    {
        summary.Status = AccountStatus.Closed.Name;
        summary.Version = e.Version;
        summary.LastActivityUtc = e.Data.OccurredOnUtc;
    }
}
