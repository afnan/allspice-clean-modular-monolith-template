namespace AllSpice.CleanModularMonolith.ApiGateway.Middleware;

/// <summary>
/// Middleware that captures unhandled exceptions and converts them to RFC 7807 JSON responses.
/// </summary>
public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;
    private readonly IWebHostEnvironment _environment;

    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorHandlingMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="logger">Logger used to record exception details.</param>
    /// <param name="environment">The current hosting environment.</param>
    public ErrorHandlingMiddleware(
        RequestDelegate next,
        ILogger<ErrorHandlingMiddleware> logger,
        IWebHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    /// <summary>
    /// Wraps downstream middleware execution in a try/catch block and normalizes exceptions.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>A task representing middleware execution.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
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

    /// <summary>
    /// Generates a problem details response for the provided exception.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="exception">The exception that was thrown.</param>
    /// <param name="correlationId">The correlation identifier associated with the request.</param>
    /// <returns>A task that writes the problem response to the pipeline.</returns>
    private Task HandleExceptionAsync(HttpContext context, Exception exception, string correlationId)
    {
        var statusCode = exception switch
        {
            UnauthorizedAccessException => HttpStatusCode.Unauthorized,
            ArgumentException => HttpStatusCode.BadRequest,
            KeyNotFoundException => HttpStatusCode.NotFound,
            TimeoutException => HttpStatusCode.RequestTimeout,
            _ => HttpStatusCode.InternalServerError
        };

        var problemDetails = new GatewayProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
            Title = "An error occurred while processing your request.",
            Status = (int)statusCode,
            Detail = _environment.IsDevelopment()
                ? exception.ToString()
                : "An error occurred while processing your request.",
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

    /// <summary>
    /// Lightweight DTO used to serialize problem detail responses.
    /// </summary>
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
