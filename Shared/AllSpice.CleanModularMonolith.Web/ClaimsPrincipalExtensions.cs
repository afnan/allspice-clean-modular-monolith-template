using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace AllSpice.CleanModularMonolith.Web;

public static class ClaimsPrincipalExtensions
{
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


