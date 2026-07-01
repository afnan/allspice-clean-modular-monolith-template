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

            // Only set the status if nothing has been written yet — mutating a started response throws.
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = 499; // nginx-style "Client Closed Request"
            }
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

            // If the response has already started (e.g. a streaming or proxied endpoint threw mid-write) the
            // status and headers are read-only; attempting to write the problem+json body would throw an
            // InvalidOperationException that masks the real exception and corrupts the response. Let the
            // original exception propagate so the host aborts the connection cleanly instead.
            if (context.Response.HasStarted)
            {
                _logger.LogWarning(
                    "Response for {Method} {Path} had already started; cannot write an RFC7807 problem body.",
                    context.Request.Method, context.Request.Path);
                throw;
            }

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
            // Kestrel throws this (StatusCode 413) when a request body exceeds the per-request
            // MaxRequestBodySize set by RequestValidationMiddleware, or 400 for a malformed request line.
            Microsoft.AspNetCore.Http.BadHttpRequestException badHttpRequest => (HttpStatusCode)badHttpRequest.StatusCode,
            _ => HttpStatusCode.InternalServerError
        };

        var isIdentityError = exception is IdentityServerUnreachableException;

        // Machine-readable error code for clients/agents: domain exceptions carry their own stable Code;
        // everything else is derived from the mapped status.
        var code = exception is DomainException domainException
            ? domainException.Code
            : CodeForStatus(statusCode);

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
            ["correlationId"] = correlationId,
            ["code"] = code
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

    private static string CodeForStatus(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.NotFound => "not_found",
        HttpStatusCode.BadRequest => "bad_request",
        HttpStatusCode.Unauthorized => "unauthorized",
        HttpStatusCode.Forbidden => "forbidden",
        HttpStatusCode.Conflict => "conflict",
        HttpStatusCode.UnprocessableEntity => "unprocessable_entity",
        HttpStatusCode.RequestTimeout => "request_timeout",
        HttpStatusCode.ServiceUnavailable => "service_unavailable",
        _ => "internal_error"
    };
}
