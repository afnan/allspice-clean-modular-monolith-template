namespace AllSpice.CleanModularMonolith.ApiGateway.Middleware;

/// <summary>
/// Single writer for the gateway's RFC 7807 <c>application/problem+json</c> error envelope. Centralised so
/// every error site — the exception handler, the request-size guard, the idempotency-conflict response, and
/// the rate-limiter rejection — emits an identical shape (<c>type</c>/<c>title</c>/<c>status</c>/<c>detail</c>
/// plus the <c>correlationId</c> extension and an optional machine-readable <c>code</c>) instead of each
/// hand-rolling a divergent body.
/// </summary>
internal static class ProblemJsonWriter
{
    private const string ProblemContentType = "application/problem+json";

    /// <summary>
    /// Writes an RFC 7807 problem+json body and sets the response status code and content type.
    /// </summary>
    /// <param name="context">The current HTTP context whose response is written.</param>
    /// <param name="status">The HTTP status code used for both the response and the <c>status</c> member.</param>
    /// <param name="title">A short, human-readable summary of the problem type.</param>
    /// <param name="detail">A human-readable explanation specific to this occurrence.</param>
    /// <param name="type">A URI reference identifying the problem type. Defaults to <c>about:blank</c>.</param>
    /// <param name="code">An optional stable, machine-readable error code (omitted when null/empty).</param>
    /// <param name="correlationId">
    /// An explicit correlation id. When null, the id is read from <see cref="HttpContext.Items"/> (populated by
    /// <see cref="CorrelationIdMiddleware"/>); the member is omitted if neither source supplies one.
    /// </param>
    /// <param name="extensions">Optional additional RFC 7807 extension members, merged at the root object.</param>
    /// <param name="cancellationToken">A token to cancel the write.</param>
    public static Task WriteAsync(
        HttpContext context,
        int status,
        string title,
        string detail,
        string type = "about:blank",
        string? code = null,
        string? correlationId = null,
        IReadOnlyDictionary<string, object?>? extensions = null,
        CancellationToken cancellationToken = default)
    {
        correlationId ??= context.Items.TryGetValue(HttpHeaderNames.CorrelationId, out var stored)
            ? stored?.ToString()
            : null;

        // A flat dictionary so extensions live at the ROOT object (RFC 7807), not nested under an
        // "extensions" node. Explicit lowercase keys are written verbatim by the serializer.
        var problem = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["type"] = type,
            ["title"] = title,
            ["status"] = status,
            ["detail"] = detail,
        };

        if (!string.IsNullOrEmpty(correlationId))
        {
            problem["correlationId"] = correlationId;
        }

        if (!string.IsNullOrEmpty(code))
        {
            problem["code"] = code;
        }

        if (extensions is not null)
        {
            foreach (var (key, value) in extensions)
            {
                problem[key] = value;
            }
        }

        context.Response.StatusCode = status;
        context.Response.ContentType = ProblemContentType;
        return context.Response.WriteAsJsonAsync(
            problem, options: null, contentType: ProblemContentType, cancellationToken: cancellationToken);
    }
}
