using AllSpice.CleanModularMonolith.ApiGateway.Middleware;
using AllSpice.CleanModularMonolith.EventSourcing;

namespace AllSpice.CleanModularMonolith.ApiGateway.Infrastructure.EventSourcing;

/// <summary>
/// Stamps every appended event with the request's correlation id (set by <see cref="CorrelationIdMiddleware"/>)
/// and the client's <c>Idempotency-Key</c>, so a stream can be tied back to a request and a replayed command
/// recognised at the stream level. Scoped: read once per request.
/// </summary>
public sealed class HttpEventMetadataProvider(IHttpContextAccessor httpContextAccessor) : IEventMetadataProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    public string? CorrelationId =>
        _httpContextAccessor.HttpContext?.Items[HttpHeaderNames.CorrelationId] as string;

    public string? IdempotencyKey =>
        _httpContextAccessor.HttpContext?.Request.Headers[IdempotencyMiddleware.HeaderName].FirstOrDefault();
}
