namespace AllSpice.CleanModularMonolith.ApiGateway.Middleware;

/// <summary>
/// Middleware that performs basic validation checks on incoming requests.
/// </summary>
public class RequestValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestValidationMiddleware> _logger;
    private const long MaxRequestSizeBytes = 10 * 1024 * 1024; // 10 MB

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestValidationMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="logger">Logger used to record validation failures.</param>
    public RequestValidationMiddleware(RequestDelegate next, ILogger<RequestValidationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Validates request metadata (e.g. payload size) before passing control downstream.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>A task representing the asynchronous middleware operation.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.ContentLength > MaxRequestSizeBytes)
        {
            _logger.LogWarning(
                "Request size {Size} exceeds maximum allowed size {MaxSize}. Request: {Path}",
                context.Request.ContentLength,
                MaxRequestSizeBytes,
                context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            await context.Response.WriteAsync("Request payload too large. Maximum size is 10 MB.");
            return;
        }

        if (context.Request.Path.StartsWithSegments("/api") &&
            context.Request.Method != HttpMethods.Options)
        {
            // Place custom header validation logic here if required by modules.
        }

        await _next(context);
    }
}


