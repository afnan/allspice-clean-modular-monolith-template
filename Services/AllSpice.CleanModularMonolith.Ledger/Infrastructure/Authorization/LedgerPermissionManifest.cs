using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;

namespace AllSpice.CleanModularMonolith.Ledger.Infrastructure.Authorization;

/// <summary>Every permission key the Ledger module enforces; seeded as IsSystem by the reconciler.</summary>
public sealed class LedgerPermissionManifest : IModulePermissionManifest
{
    public string ModuleKey => "ledger";

    public IReadOnlyCollection<PermissionDefinition> Permissions =>
    [
        new("ledger.access", "Access the ledger module"),
        new("ledger:accounts.read", "View ledger accounts and their history"),
        new("ledger:accounts.write", "Open, close and move funds on ledger accounts"),
    ];
}
