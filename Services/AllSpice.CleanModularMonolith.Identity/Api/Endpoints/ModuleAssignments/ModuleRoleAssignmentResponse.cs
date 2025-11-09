namespace AllSpice.CleanModularMonolith.Identity.Api.Endpoints.ModuleAssignments;

public sealed record ModuleRoleAssignmentResponse(
    Guid AssignmentId,
    string UserId,
    string ModuleKey,
    string RoleKey,
    DateTimeOffset AssignedUtc,
    DateTimeOffset? RevokedUtc);


