namespace AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;

/// <summary>A single permission a module declares it enforces: a stable key plus a human-readable description.</summary>
public sealed record PermissionDefinition(string Key, string Description);
