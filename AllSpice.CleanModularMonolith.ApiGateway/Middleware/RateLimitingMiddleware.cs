namespace AllSpice.CleanModularMonolith.ApiGateway.Middleware;

/// <summary>
/// Middleware that enforces rate limiting using an injected <see cref="RateLimiter"/>.
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly RateLimiter _rateLimiter;

    /// <summary>
    /// Initializes a new instance of the <see cref="RateLimitingMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="rateLimiter">The shared rate limiter used to govern throughput.</param>
    public RateLimitingMiddleware(RequestDelegate next, RateLimiter rateLimiter)
    {
        _next = next;
        _rateLimiter = rateLimiter;
    }

    /// <summary>
    /// Attempts to acquire a rate limit permit before processing the request.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>A task representing the asynchronous middleware execution.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        var lease = await _rateLimiter.AcquireAsync(permitCount: 1, context.RequestAborted);

        if (!lease.IsAcquired)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers["Retry-After"] = "60";
            await context.Response.WriteAsync("Rate limit exceeded. Please try again later.");
            return;
        }

        try
        {
            await _next(context);
        }
        finally
        {
            lease.Dispose();
        }
    }
}


