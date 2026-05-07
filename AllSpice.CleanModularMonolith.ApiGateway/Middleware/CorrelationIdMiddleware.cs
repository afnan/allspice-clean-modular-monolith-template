using System.Text.RegularExpressions;

namespace AllSpice.CleanModularMonolith.ApiGateway.Middleware;

/// <summary>
/// Middleware that ensures each request/response pair carries a correlation identifier.
/// Caller-supplied IDs are validated to a conservative alphanumeric+dash+underscore
/// pattern so a hostile header can't inject CRLF or other control characters into the
/// response stream.
/// </summary>
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private const int MaxCorrelationIdLength = 64;

    private static readonly Regex AllowedPattern = new(
        @"^[A-Za-z0-9_\-]{1,64}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Assigns or propagates a correlation identifier for the current HTTP request.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var supplied = context.Request.Headers[HttpHeaderNames.CorrelationId].FirstOrDefault();

        var correlationId = !string.IsNullOrWhiteSpace(supplied) && IsValid(supplied)
            ? supplied
            : Guid.NewGuid().ToString("N");

        context.Items[HttpHeaderNames.CorrelationId] = correlationId;
        context.Response.Headers[HttpHeaderNames.CorrelationId] = correlationId;

        await _next(context);
    }

    private static bool IsValid(string value)
        => value.Length <= MaxCorrelationIdLength && AllowedPattern.IsMatch(value);
}
