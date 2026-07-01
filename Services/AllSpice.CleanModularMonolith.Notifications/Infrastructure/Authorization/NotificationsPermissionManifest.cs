using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Authorization;

/// <summary>
/// Declares every permission key the Notifications module enforces.
/// The reconciler (AuthorizationCatalogReconciler) seeds these as IsSystem records so they
/// survive across deployments and cannot be deleted through the admin API.
/// </summary>
public sealed class NotificationsPermissionManifest : IModulePermissionManifest
{
    public string ModuleKey => "notifications";

    public IReadOnlyCollection<PermissionDefinition> Permissions =>
    [
        new("notifications.access", "Access the notifications module"),
        new("notifications:preferences.manage", "Manage notification preferences"),
    ];
}
