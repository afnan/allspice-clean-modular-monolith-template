namespace AllSpice.CleanModularMonolith.Identity.Application.DTOs;

public sealed record UserDto(
    Guid Id,
    string ExternalId,
    string Username,
    string? Email,
    string? FirstName,
    string? LastName,
    string DisplayName,
    bool IsActive,
    DateTimeOffset LastSyncedUtc);
