using Microsoft.AspNetCore.Http.Features;

namespace AllSpice.CleanModularMonolith.ApiGateway.Middleware;

/// <summary>
/// Middleware that performs basic validation checks on incoming requests.
/// </summary>
public class RequestValidationMiddleware(RequestDelegate next, ILogger<RequestValidationMiddleware> logger)
{
    private readonly RequestDelegate _next = next;
    private readonly ILogger<RequestValidationMiddleware> _logger = logger;
    private const long MaxRequestSizeBytes = 10 * 1024 * 1024; // 10 MB

    /// <summary>
    /// Validates request metadata (e.g. payload size) before passing control downstream.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>A task representing the asynchronous middleware operation.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        // Enforce the cap on the BODY STREAM itself (covers chunked/streamed requests that omit
        // Content-Length). A Content-Length-only check is bypassable: a client using
        // "Transfer-Encoding: chunked" reports a null length, so the fast-path below is skipped and only
        // Kestrel's larger global default would apply. Setting the per-request feature makes the server
        // abort the read with a 413 once the limit is crossed, regardless of how the body is framed.
        var maxBodySizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (maxBodySizeFeature is { IsReadOnly: false })
        {
            maxBodySizeFeature.MaxRequestBodySize = MaxRequestSizeBytes;
        }

        // Fast path: reject an over-large *declared* Content-Length before the body is even read.
        if (context.Request.ContentLength > MaxRequestSizeBytes)
        {
            _logger.LogWarning(
                "Request size {Size} exceeds maximum allowed size {MaxSize}. Request: {Path}",
                context.Request.ContentLength,
                MaxRequestSizeBytes,
                context.Request.Path);

            await WritePayloadTooLargeAsync(context);
            return;
        }

        if (context.Request.Path.StartsWithSegments("/api") &&
            context.Request.Method != HttpMethods.Options)
        {
            // Place custom header validation logic here if required by modules.
        }

        await _next(context);
    }

    /// <summary>
    /// Writes a 413 as RFC7807 <c>application/problem+json</c>, matching the gateway's error contract used by
    /// <see cref="ErrorHandlingMiddleware"/> and the mediator/FastEndpoints path (instead of a bare string).
    /// </summary>
    private static Task WritePayloadTooLargeAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
        var problem = new
        {
            type = "https://tools.ietf.org/html/rfc9110#section-15.5.14",
            title = "Payload too large",
            status = StatusCodes.Status413PayloadTooLarge,
            detail = "Request payload too large. Maximum size is 10 MB."
        };
        return context.Response.WriteAsJsonAsync(
            problem, options: null, contentType: "application/problem+json", cancellationToken: context.RequestAborted);
    }
}
