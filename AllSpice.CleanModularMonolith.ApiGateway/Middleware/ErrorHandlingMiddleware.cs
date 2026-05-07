using AllSpice.CleanModularMonolith.SharedKernel.Exceptions;

namespace AllSpice.CleanModularMonolith.ApiGateway.Middleware;

/// <summary>
/// Middleware that captures unhandled exceptions and converts them to RFC 7807 JSON responses.
/// Maps domain exception types to appropriate HTTP status codes.
/// </summary>
public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;
    private readonly IWebHostEnvironment _environment;

    public ErrorHandlingMiddleware(
        RequestDelegate next,
        ILogger<ErrorHandlingMiddleware> logger,
        IWebHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

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
            var correlationId = context.Items["X-Correlation-ID"]?.ToString() ?? "unknown";

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

        var problemDetails = new GatewayProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
            Title = isIdentityError
                ? "Identity service is temporarily unavailable."
                : "An error occurred while processing your request.",
            Status = (int)statusCode,
            // Keep the development Detail short — exception.ToString() can include
            // connection strings, JWTs, and other secrets in inner-exception messages.
            // The full exception is already in the structured log (LogError above);
            // dev consumers can correlate via the correlationId extension.
            Detail = _environment.IsDevelopment()
                ? $"{exception.GetType().Name}: {Truncate(exception.Message, 512)}"
                : (isIdentityError ? "The identity provider is unreachable. Please try again later." : "An error occurred while processing your request."),
            Instance = context.Request.Path
        };

        problemDetails.Extensions["correlationId"] = correlationId;

        if (_environment.IsDevelopment())
        {
            problemDetails.Extensions["exception"] = exception.GetType().Name;
            problemDetails.Extensions["stackTrace"] = exception.StackTrace;
        }

        var result = JsonSerializer.Serialize(problemDetails, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)statusCode;
        return context.Response.WriteAsync(result);
    }

    private static string Truncate(string value, int maxLength)
        => value is null
            ? string.Empty
            : value.Length <= maxLength ? value : value[..maxLength];

    private sealed class GatewayProblemDetails
    {
        public string Type { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public int Status { get; init; }
        public string Detail { get; init; } = string.Empty;
        public string Instance { get; init; } = string.Empty;
        public IDictionary<string, object?> Extensions { get; } = new Dictionary<string, object?>(StringComparer.Ordinal);
    }
}
