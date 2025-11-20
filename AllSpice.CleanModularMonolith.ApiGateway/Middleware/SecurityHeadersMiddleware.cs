namespace AllSpice.CleanModularMonolith.ApiGateway.Middleware;

/// <summary>
/// Middleware that appends baseline security headers to outgoing responses.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecurityHeadersMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Appends security-related HTTP response headers and continues request processing.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>A task that completes when the downstream middleware finishes executing.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("X-Frame-Options", "DENY");
        context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");

        if (context.Request.IsHttps)
        {
            context.Response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
        }

        context.Response.Headers.Append(
            "Content-Security-Policy",
            "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'");

        await _next(context);
    }
}


