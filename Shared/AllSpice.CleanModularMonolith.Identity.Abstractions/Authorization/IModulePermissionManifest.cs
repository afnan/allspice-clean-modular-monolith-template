namespace AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;

/// <summary>A module declares the permission keys it enforces. The reconciler seeds these as IsSystem.
/// Convention: a coarse "<c>{module}.access</c>" gate plus fine "<c>{module}:area.action</c>" keys.</summary>
public interface IModulePermissionManifest
{
    string ModuleKey { get; }
    IReadOnlyCollection<PermissionDefinition> Permissions { get; }
}
