namespace AllSpice.CleanModularMonolith.Identity.Application.DTOs;

/// <summary>
/// Data transfer object representing a module role assignment.
/// </summary>
/// <param name="AssignmentId">Identifier of the assignment.</param>
/// <param name="UserId">External user identifier.</param>
/// <param name="ModuleKey">Module key tied to the assignment.</param>
/// <param name="RoleKey">Role key granted to the user.</param>
/// <param name="AssignedUtc">UTC timestamp when the assignment started.</param>
/// <param name="RevokedUtc">UTC timestamp when the assignment was revoked, if any.</param>
public sealed record ModuleRoleAssignmentDto(
    Guid AssignmentId,
    string UserId,
    string ModuleKey,
    string RoleKey,
    DateTimeOffset AssignedUtc,
    DateTimeOffset? RevokedUtc);

