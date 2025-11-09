namespace AllSpice.CleanModularMonolith.Identity.Application.DTOs;

public sealed record ModuleRoleAssignmentDto(
    Guid AssignmentId,
    string UserId,
    string ModuleKey,
    string RoleKey,
    DateTimeOffset AssignedUtc,
    DateTimeOffset? RevokedUtc);


