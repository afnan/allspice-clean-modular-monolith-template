namespace AllSpice.CleanModularMonolith.ApiGateway.Extensions;

/// <summary>
/// Provides extensions that encapsulate the API gateway's middleware pipeline configuration.
/// </summary>
public static class GatewayApplicationExtensions
{
    /// <summary>
    /// Adds the standard middleware pipeline used by the gateway (security, diagnostics, caching, auth, etc.).
    /// </summary>
    /// <param name="app">The web application instance.</param>
    public static void UseGatewayPipeline(this WebApplication app)
    {
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<RequestValidationMiddleware>();
        app.UseResponseCompression();
        app.UseMiddleware<ErrorHandlingMiddleware>();
        app.UseCors();

        if (!app.Environment.IsDevelopment())
        {
            app.UseHttpsRedirection();
        }

        app.UseRateLimiter();
        app.UseOutputCache();
        app.UseRequestBuffering();
        app.UseAuthentication();
        app.UseAuthorization();
    }

    /// <summary>
    /// Registers the reverse proxy endpoint with logging wrapped around each proxied request.
    /// </summary>
    /// <param name="app">The web application instance.</param>
    public static void MapGatewayReverseProxy(this WebApplication app)
    {
        app.MapReverseProxy(proxyPipeline =>
        {
            proxyPipeline.Use(async (context, next) =>
            {
                var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
                var correlationId = context.Items["X-Correlation-ID"]?.ToString() ?? "unknown";

                var stopwatch = Stopwatch.StartNew();

                logger.LogInformation(
                    "Proxying request {Method} {Path} with CorrelationId: {CorrelationId}",
                    context.Request.Method,
                    context.Request.Path,
                    correlationId);

                await next();

                stopwatch.Stop();

                logger.LogInformation(
                    "Proxied response {StatusCode} for {Method} {Path} with CorrelationId: {CorrelationId} in {ElapsedMs}ms",
                    context.Response.StatusCode,
                    context.Request.Method,
                    context.Request.Path,
                    correlationId,
                    stopwatch.ElapsedMilliseconds);
            });
        });
    }

    /// <summary>
    /// Enables request buffering so downstream middleware can re-read the body if necessary.
    /// </summary>
    /// <param name="app">The application builder.</param>
    private static void UseRequestBuffering(this IApplicationBuilder app)
    {
        app.Use(async (context, next) =>
        {
            context.Request.EnableBuffering();
            context.Request.Body.Position = 0;
            await next();
        });
    }
}


