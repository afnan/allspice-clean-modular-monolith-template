using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using Ardalis.Result;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Authorization;

/// <summary>Thin facade over the built-in resource-based authorization. Dispatches to registered
/// <c>AuthorizationHandler&lt;OperationAuthorizationRequirement, TResource&gt;</c> rules and maps the verdict
/// to an Ardalis <see cref="Result"/>. Sources the principal from HttpContext so command handlers stay clean.</summary>
public sealed class ResourceAuthorizer(IAuthorizationService authorizationService, IHttpContextAccessor httpContextAccessor)
    : IResourceAuthorizer
{
    private readonly IAuthorizationService _authorizationService = authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    public async Task<Result> AuthorizeAsync<TResource>(TResource resource, string action, CancellationToken cancellationToken)
        where TResource : notnull
    {
        var user = _httpContextAccessor.HttpContext?.User ?? new System.Security.Claims.ClaimsPrincipal();
        var requirement = new OperationAuthorizationRequirement { Name = action };
        var result = await _authorizationService.AuthorizeAsync(user, resource, requirement);
        return result.Succeeded ? Result.Success() : Result.Forbidden();
    }
}
