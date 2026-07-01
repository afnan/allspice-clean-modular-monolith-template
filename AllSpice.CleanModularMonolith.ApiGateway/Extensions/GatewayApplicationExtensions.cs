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
    /// <remarks>The swagger UI title is derived from <c>Application:Name</c> configuration when available.</remarks>
    public static void UseGatewayPipeline(this WebApplication app)
    {
        // ErrorHandlingMiddleware is outermost so that exceptions thrown in SecurityHeaders/CorrelationId
        // (and everything downstream) are still rendered as RFC7807 problem+json rather than a bare 500.
        app.UseMiddleware<ErrorHandlingMiddleware>();
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<RequestValidationMiddleware>();
        app.UseResponseCompression();
        app.UseCors();

        if (!app.Environment.IsDevelopment())
        {
            app.UseHttpsRedirection();
        }

        app.UseRateLimiter();
        app.UseOutputCache();
        app.UseRequestBuffering();

        var authenticationState = app.Services.GetService<GatewayAuthenticationState>();
        if (authenticationState?.Enabled == true)
        {
            app.UseAuthentication();
        }

        if (app.Environment.IsDevelopment())
        {
            var configuration = app.Services.GetRequiredService<IConfiguration>();
            var applicationName = configuration["Application:Name"] ?? "API Gateway";

            app.UseSwaggerGen();
            app.UseSwaggerUi(options =>
            {
                options.ConfigureDefaults();
                options.DocumentTitle = applicationName;
                options.Path = "/swagger";
            });
        }

        app.UseAuthorization();

        // Resolve the authenticated subject to the canonical local user id (once per request) so audit
        // stamping records the local UUID. Must run after authentication populates HttpContext.User.
        app.UseMiddleware<CurrentUserResolutionMiddleware>();

        // Opt-in HTTP idempotency for POST/PUT/PATCH carrying an Idempotency-Key header. Runs after auth so
        // the cache key can be scoped to the subject; wraps endpoint execution to capture/replay the response.
        app.UseMiddleware<IdempotencyMiddleware>();

        app.UseFastEndpoints();
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
                var correlationId = context.Items[HttpHeaderNames.CorrelationId]?.ToString() ?? "unknown";

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


