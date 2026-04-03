namespace AllSpice.CleanModularMonolith.Identity.Api.Endpoints.Invitations;

public sealed record InviteUserRequest(
    string Email,
    string FirstName,
    string LastName,
    string? Role);
