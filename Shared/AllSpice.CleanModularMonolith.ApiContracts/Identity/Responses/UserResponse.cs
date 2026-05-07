namespace AllSpice.CleanModularMonolith.ApiContracts.Identity.Responses;

/// <summary>
/// Public response shape for a synced identity user.
/// </summary>
public sealed record UserResponse(
    Guid Id,
    string ExternalId,
    string Username,
    string? Email,
    string? FirstName,
    string? LastName,
    string DisplayName,
    bool IsActive,
    DateTimeOffset LastSyncedUtc);
