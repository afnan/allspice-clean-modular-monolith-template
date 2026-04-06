using Ardalis.SmartEnum;

namespace AllSpice.CleanModularMonolith.Identity.Domain.Enums;

public sealed class InvitationStatus : SmartEnum<InvitationStatus>
{
    public static readonly InvitationStatus Pending = new(nameof(Pending), 1);
    public static readonly InvitationStatus Accepted = new(nameof(Accepted), 2);
    public static readonly InvitationStatus Expired = new(nameof(Expired), 3);
    public static readonly InvitationStatus Cancelled = new(nameof(Cancelled), 4);

    private InvitationStatus(string name, int value) : base(name, value)
    {
    }
}
