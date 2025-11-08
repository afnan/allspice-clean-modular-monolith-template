using System.Threading.RateLimiting;

namespace AllSpice.CleanModularMonolith.ApiGateway.Middleware;

public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly RateLimiter _rateLimiter;

    public RateLimitingMiddleware(
        RequestDelegate next,
        RateLimiter rateLimiter)
    {
        _next = next;
        _rateLimiter = rateLimiter;
    }

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


