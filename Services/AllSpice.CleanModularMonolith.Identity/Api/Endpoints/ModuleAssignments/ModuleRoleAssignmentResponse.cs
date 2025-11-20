namespace AllSpice.CleanModularMonolith.Identity.Api.Endpoints.ModuleAssignments;

/// <summary>
/// Response returned when a module role assignment is returned to the client.
/// </summary>
/// <param name="AssignmentId">Identifier of the assignment.</param>
/// <param name="UserId">External user identifier.</param>
/// <param name="ModuleKey">Module key associated with the role.</param>
/// <param name="RoleKey">Role key assigned to the user.</param>
/// <param name="AssignedUtc">Timestamp (UTC) when the assignment became active.</param>
/// <param name="RevokedUtc">Timestamp (UTC) when the assignment was revoked, if applicable.</param>
public sealed record ModuleRoleAssignmentResponse(
    Guid AssignmentId,
    string UserId,
    string ModuleKey,
    string RoleKey,
    DateTimeOffset AssignedUtc,
    DateTimeOffset? RevokedUtc);

