using System;
using System.Collections.Generic;
using System.Linq;
using Ardalis.Result;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace AllSpice.CleanModularMonolith.Web;

public static class ArdalisResultHttpExtensions
{
    public static ValidationProblem ToValidationProblem(this Result result)
    {
        return CreateValidationProblem(result.ValidationErrors, result.Errors);
    }

    public static ValidationProblem ToValidationProblem<T>(this Result<T> result)
    {
        return CreateValidationProblem(result.ValidationErrors, result.Errors);
    }

    public static ProblemHttpResult ToProblem(this Result result, int statusCode, string? title = null)
    {
        return CreateProblem(result.Errors, statusCode, title);
    }

    public static ProblemHttpResult ToProblem(this Result result) =>
        result.ToProblem(StatusCodes.Status500InternalServerError);

    public static ProblemHttpResult ToProblem<T>(this Result<T> result, int statusCode, string? title = null)
    {
        return CreateProblem(result.Errors, statusCode, title);
    }

    public static ProblemHttpResult ToProblem<T>(this Result<T> result) =>
        result.ToProblem(StatusCodes.Status500InternalServerError);

    /// <summary>
    /// Writes the appropriate RFC7807 ProblemDetails response for a failed <see cref="Result"/>,
    /// mapping <see cref="ResultStatus"/> to the conventional HTTP status code. Use from an endpoint
    /// when a result is unsuccessful so each handler doesn't hand-roll the status switch.
    /// </summary>
    public static Task ExecuteFailureAsync(this Result result, HttpContext httpContext) =>
        result.Status switch
        {
            ResultStatus.Invalid => result.ToValidationProblem().ExecuteAsync(httpContext),
            ResultStatus.NotFound => result.ToProblem(StatusCodes.Status404NotFound).ExecuteAsync(httpContext),
            ResultStatus.Conflict => result.ToProblem(StatusCodes.Status409Conflict).ExecuteAsync(httpContext),
            ResultStatus.Forbidden => result.ToProblem(StatusCodes.Status403Forbidden).ExecuteAsync(httpContext),
            ResultStatus.Unauthorized => result.ToProblem(StatusCodes.Status401Unauthorized).ExecuteAsync(httpContext),
            ResultStatus.Error => result.ToProblem(StatusCodes.Status400BadRequest).ExecuteAsync(httpContext),
            _ => result.ToProblem(StatusCodes.Status500InternalServerError).ExecuteAsync(httpContext)
        };

    /// <inheritdoc cref="ExecuteFailureAsync(Result, HttpContext)"/>
    public static Task ExecuteFailureAsync<T>(this Result<T> result, HttpContext httpContext) =>
        result.Status switch
        {
            ResultStatus.Invalid => result.ToValidationProblem().ExecuteAsync(httpContext),
            ResultStatus.NotFound => result.ToProblem(StatusCodes.Status404NotFound).ExecuteAsync(httpContext),
            ResultStatus.Conflict => result.ToProblem(StatusCodes.Status409Conflict).ExecuteAsync(httpContext),
            ResultStatus.Forbidden => result.ToProblem(StatusCodes.Status403Forbidden).ExecuteAsync(httpContext),
            ResultStatus.Unauthorized => result.ToProblem(StatusCodes.Status401Unauthorized).ExecuteAsync(httpContext),
            ResultStatus.Error => result.ToProblem(StatusCodes.Status400BadRequest).ExecuteAsync(httpContext),
            _ => result.ToProblem(StatusCodes.Status500InternalServerError).ExecuteAsync(httpContext)
        };

    private static ValidationProblem CreateValidationProblem(IEnumerable<ValidationError>? validationErrors, IEnumerable<string> errors)
    {
        var dictionary = validationErrors?
            .GroupBy(error => error.Identifier ?? string.Empty)
            .ToDictionary(
                group => string.IsNullOrWhiteSpace(group.Key) ? string.Empty : group.Key,
                group => group.Select(error => error.ErrorMessage).ToArray())
            ?? new Dictionary<string, string[]>();

        var errorArray = errors?.Where(message => !string.IsNullOrWhiteSpace(message)).ToArray() ?? Array.Empty<string>();

        if (dictionary.Count == 0 && errorArray.Length > 0)
        {
            dictionary[string.Empty] = errorArray;
        }

        return TypedResults.ValidationProblem(dictionary);
    }

    private static ProblemHttpResult CreateProblem(IEnumerable<string> errors, int statusCode, string? title)
    {
        var sanitizedErrors = errors?.Where(message => !string.IsNullOrWhiteSpace(message)).ToArray() ?? Array.Empty<string>();

        var detail = sanitizedErrors.Any()
            ? string.Join(Environment.NewLine, sanitizedErrors)
            : null;

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title ?? GetDefaultTitle(statusCode),
            Detail = detail
        };

        return TypedResults.Problem(problem);
    }

    private static string GetDefaultTitle(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "Bad Request",
        StatusCodes.Status404NotFound => "Not Found",
        StatusCodes.Status409Conflict => "Conflict",
        StatusCodes.Status500InternalServerError => "Internal Server Error",
        _ => "Error"
    };
}


