namespace AllSpice.CleanModularMonolith.ApiGateway.Middleware;

public class RequestValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestValidationMiddleware> _logger;
    private const long MaxRequestSizeBytes = 10 * 1024 * 1024; // 10 MB

    public RequestValidationMiddleware(RequestDelegate next, ILogger<RequestValidationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Validate request size
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

        // Validate required headers for API requests
        if (context.Request.Path.StartsWithSegments("/api") && 
            context.Request.Method != "OPTIONS")
        {
            // Add any custom header validation here
            // Example: Check for required headers
        }

        await _next(context);
    }
}


