using Ardalis.SmartEnum;

namespace AllSpice.CleanModularMonolith.Ledger.Domain.Enums;

public sealed class AccountStatus : SmartEnum<AccountStatus>
{
    public static readonly AccountStatus Open = new(nameof(Open), 1);
    public static readonly AccountStatus Closed = new(nameof(Closed), 2);

    private AccountStatus(string name, int value)
        : base(name, value)
    {
    }
}
