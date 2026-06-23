namespace AllSpice.CleanModularMonolith.ApiContracts.Identity.Responses;

/// <summary>
/// Standard error response returned by identity endpoints.
/// </summary>
/// <param name="Errors">Collection of error messages.</param>
public sealed record IdentityErrorResponse(IReadOnlyCollection<string> Errors);
