using AllSpice.CleanModularMonolith.SharedKernel.Common;
using AllSpice.CleanModularMonolith.SharedKernel.Exceptions;

namespace AllSpice.CleanModularMonolith.ApiGateway.Middleware;

/// <summary>
/// Middleware that captures unhandled exceptions and converts them to RFC 7807 JSON responses.
/// Maps domain exception types to appropriate HTTP status codes.
/// </summary>
public class ErrorHandlingMiddleware(
    RequestDelegate next,
    ILogger<ErrorHandlingMiddleware> logger,
    IWebHostEnvironment environment)
{
    private readonly RequestDelegate _next = next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger = logger;
    private readonly IWebHostEnvironment _environment = environment;

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogDebug("Request cancelled by client: {Method} {Path}",
                context.Request.Method, context.Request.Path);
            context.Response.StatusCode = 499; // nginx-style "Client Closed Request"
        }
        catch (Exception ex)
        {
            var correlationId = context.Items[HttpHeaderNames.CorrelationId]?.ToString() ?? "unknown";

            _logger.LogError(
                ex,
                "An unhandled exception occurred. CorrelationId: {CorrelationId}, Path: {Path}, Method: {Method}",
                correlationId,
                context.Request.Path,
                context.Request.Method);

            await HandleExceptionAsync(context, ex, correlationId);
        }
    }

    private Task HandleExceptionAsync(HttpContext context, Exception exception, string correlationId)
    {
        var statusCode = exception switch
        {
            NotFoundException => HttpStatusCode.NotFound,
            DomainValidationException => HttpStatusCode.BadRequest,
            FluentValidation.ValidationException => HttpStatusCode.BadRequest,
            UnauthorizedException => HttpStatusCode.Unauthorized,
            ForbiddenException => HttpStatusCode.Forbidden,
            ConflictException => HttpStatusCode.Conflict,
            BusinessRuleViolationException => HttpStatusCode.UnprocessableEntity,
            IdentityServerUnreachableException => HttpStatusCode.ServiceUnavailable,
            UnauthorizedAccessException => HttpStatusCode.Unauthorized,
            ArgumentException => HttpStatusCode.BadRequest,
            KeyNotFoundException => HttpStatusCode.NotFound,
            TimeoutException => HttpStatusCode.RequestTimeout,
            _ => HttpStatusCode.InternalServerError
        };

        var isIdentityError = exception is IdentityServerUnreachableException;

        // RFC7807: members (incl. extensions like correlationId/errors) live at the ROOT object, matching
        // the validation-problem shape returned by the mediator/FastEndpoints path. We build a flat dictionary
        // rather than nesting under an "extensions" object (which the previous shape did, diverging from RFC7807).
        var problem = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["type"] = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
            ["title"] = isIdentityError
                ? "Identity service is temporarily unavailable."
                : "An error occurred while processing your request.",
            ["status"] = (int)statusCode,
            // Keep the development Detail short — exception.ToString() can include connection strings, JWTs,
            // and other secrets in inner-exception messages. The full exception is already in the structured
            // log (LogError above); dev consumers can correlate via the correlationId member.
            ["detail"] = _environment.IsDevelopment()
                ? $"{exception.GetType().Name}: {exception.Message.Truncate(512)}"
                : (isIdentityError ? "The identity provider is unreachable. Please try again later." : "An error occurred while processing your request."),
            ["instance"] = context.Request.Path.Value,
            ["correlationId"] = correlationId
        };

        // Surface field-level validation errors so clients get actionable 400s. This is the defensive
        // net: the Mediator pipeline normally maps ValidationException to Result.Invalid first.
        if (exception is FluentValidation.ValidationException validationException)
        {
            problem["errors"] = validationException.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
        }

        if (_environment.IsDevelopment())
        {
            problem["exception"] = exception.GetType().Name;
            problem["stackTrace"] = exception.StackTrace;
        }

        var result = JsonSerializer.Serialize(problem);

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)statusCode;
        return context.Response.WriteAsync(result);
    }
}
