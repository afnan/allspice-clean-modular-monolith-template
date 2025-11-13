using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace AllSpice.CleanModularMonolith.Web;

public static class ClaimsPrincipalExtensions
{
    public static string? GetUserObjectId(this ClaimsPrincipal principal)
    {
        return principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? principal.FindFirst("sub")?.Value
            ?? principal.FindFirst("oid")?.Value
            ?? principal.FindFirst("client_id")?.Value;
    }

    public static ProblemHttpResult ToUnauthorizedProblem(this ClaimsPrincipal principal, string? detail = null)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Unauthorized",
            Detail = detail ?? "Unable to resolve the current user identity."
        };

        return TypedResults.Problem(problem);
    }
}


