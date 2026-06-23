namespace AllSpice.CleanModularMonolith.ApiGateway.Middleware;

/// <summary>
/// HTTP header names used by gateway middleware. Centralized so a rename in one place
/// can't silently break the consumer in another.
/// </summary>
internal static class HttpHeaderNames
{
    public const string CorrelationId = "X-Correlation-ID";
}
