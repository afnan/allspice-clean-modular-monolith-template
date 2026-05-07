using AllSpice.CleanModularMonolith.ApiContracts.Identity.Responses;
using AllSpice.CleanModularMonolith.Identity.Application.DTOs;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.User;

namespace AllSpice.CleanModularMonolith.Identity.Application.Mappers;

public static class UserMapper
{
    public static UserDto ToDto(User user) => new(
        user.Id,
        user.ExternalId.Value,
        user.Username,
        user.Email,
        user.FirstName,
        user.LastName,
        user.DisplayName,
        user.IsActive,
        user.LastSyncedUtc);

    public static UserResponse ToResponse(UserDto dto) => new(
        dto.Id,
        dto.ExternalId,
        dto.Username,
        dto.Email,
        dto.FirstName,
        dto.LastName,
        dto.DisplayName,
        dto.IsActive,
        dto.LastSyncedUtc);
}
