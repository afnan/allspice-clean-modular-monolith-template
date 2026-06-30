namespace AllSpice.CleanModularMonolith.Identity.Application.Features.Authorization;

public sealed record PermissionDto(Guid Id, string Key, string Description, bool IsSystem);
