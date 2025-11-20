namespace AllSpice.CleanModularMonolith.Identity.Api.Responses;

/// <summary>
/// Standard error response returned by identity endpoints.
/// </summary>
/// <param name="Errors">Collection of error messages.</param>
public sealed record IdentityErrorResponse(string[] Errors);

