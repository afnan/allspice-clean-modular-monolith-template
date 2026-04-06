using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.User;

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
    DateTimeOffset LastSyncedUtc)
{
    public static UserDto From(User user) => new(
        user.Id,
        user.ExternalId.Value,
        user.Username,
        user.Email,
        user.FirstName,
        user.LastName,
        user.DisplayName,
        user.IsActive,
        user.LastSyncedUtc);
}
