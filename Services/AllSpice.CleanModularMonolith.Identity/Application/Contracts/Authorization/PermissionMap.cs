namespace AllSpice.CleanModularMonolith.Identity.Application.Contracts.Authorization;

/// <summary>A snapshot of the whole role→permission map plus the version it was built at.</summary>
public sealed record PermissionMap(long Version, IReadOnlyDictionary<string, IReadOnlySet<string>> RoleToPermissions);
