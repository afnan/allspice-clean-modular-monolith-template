using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace AllSpice.CleanModularMonolith.ApiGateway.Middleware;

/// <summary>
/// Opt-in HTTP idempotency for unsafe, body-bearing methods (POST/PUT/PATCH). When a client sends an
/// <c>Idempotency-Key</c> header, the first successful (2xx) response is cached (Redis-backed
/// <see cref="IDistributedCache"/>) and replayed verbatim for any retry with the same key — so a network
/// retry can't create a duplicate resource. A retry that arrives while the original is still in flight gets
/// <c>409 Conflict</c>. Requests without the header are unaffected.
/// </summary>
/// <remarks>
/// Scope: the cache key is derived from method + path + authenticated subject + the client key, so the same
/// key from different users/endpoints never collides. Only 2xx responses are cached; non-success responses
/// are not, so the client can retry them fresh. Complements the durable outbox (which dedupes *messages*);
/// this dedupes *HTTP commands*.
/// <para>
/// <b>TOCTOU caveat (best-effort):</b> at-most-once is guaranteed only for retries after the in-progress
/// marker has been written; two simultaneous first requests with the same key can both pass the null-check
/// before either sets the marker. The implementation prioritises simplicity over strict deduplication of the
/// very first request.
/// <!-- TODO: upgrade to atomic Redis SET key val NX (via IDistributedLockFactory or raw StackExchange.Redis)
///      for strict first-request de-dup, eliminating the TOCTOU window entirely. -->
/// </para>
/// </remarks>
public sealed class IdempotencyMiddleware(
    RequestDelegate next,
    IDistributedCache cache,
    ILogger<IdempotencyMiddleware> logger)
{
    public const string HeaderName = "Idempotency-Key";
    public const string ReplayedHeader = "Idempotency-Replayed";

    private static readonly TimeSpan CompletedTtl = TimeSpan.FromHours(24);
    private static readonly TimeSpan InProgressTtl = TimeSpan.FromMinutes(5);

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        var method = context.Request.Method;
        var isMutating = HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method);

        if (!isMutating ||
            !context.Request.Headers.TryGetValue(HeaderName, out var keyValues) ||
            string.IsNullOrWhiteSpace(keyValues))
        {
            await next(context);
            return;
        }

        // Use the stable, unique identity claim (not the display name, which can be null even for authenticated
        // users and would cause two authenticated users to share the "anonymous" key namespace).
        var subject = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub")
            ?? "anonymous";
        var cacheKey = BuildKey(method, context.Request.Path, subject, keyValues.ToString());

        var existingBytes = await cache.GetAsync(cacheKey, context.RequestAborted);
        if (existingBytes is not null)
        {
            var existing = Deserialize(existingBytes);
            if (existing is null || existing.InProgress)
            {
                await WriteInProgressConflictAsync(context);
                return;
            }

            await ReplayAsync(context, existing);
            logger.LogInformation("Replayed idempotent response for key {Key} ({Status}).", cacheKey, existing.StatusCode);
            return;
        }

        // Best-effort in-flight marker so a concurrent retry gets 409 rather than double-processing.
        // See TOCTOU caveat in the class remarks — two simultaneous first requests can both pass this check.
        await cache.SetAsync(
            cacheKey,
            Serialize(new CachedResponse { InProgress = true }),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = InProgressTtl },
            context.RequestAborted);

        var originalBody = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        // Tracks whether a completed 2xx response was written to the cache. Used in the catch block to
        // determine whether to remove the cache entry: we only remove the in-progress marker, never a
        // completed cached response (removing a completed entry would force the next retry to re-execute
        // the command even though it already succeeded).
        var completedStored = false;

        try
        {
            await next(context);

            var bodyBytes = buffer.ToArray();

            if (context.Response.StatusCode is >= 200 and < 300)
            {
                // Capture response headers, excluding hop-by-hop / recomputed headers so they are
                // not double-applied when the response is replayed (Content-Type is restored via
                // the ContentType property; Content-Length and Transfer-Encoding are recomputed).
                var headers = context.Response.Headers
                    .Where(kv => !string.IsNullOrEmpty(kv.Key) &&
                                 !kv.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) &&
                                 !kv.Key.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
                    .ToDictionary(
                        kv => kv.Key,
                        kv => kv.Value.Where(v => v is not null).Select(v => v!).ToArray(),
                        StringComparer.OrdinalIgnoreCase);

                var stored = new CachedResponse
                {
                    StatusCode = context.Response.StatusCode,
                    ContentType = context.Response.ContentType,
                    Body = bodyBytes,
                    Headers = headers,
                    InProgress = false
                };
                await cache.SetAsync(
                    cacheKey,
                    Serialize(stored),
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CompletedTtl },
                    context.RequestAborted);
                completedStored = true;
            }
            else
            {
                // Don't cache failures — let the client retry fresh.
                await cache.RemoveAsync(cacheKey, context.RequestAborted);
            }

            context.Response.Body = originalBody;
            await originalBody.WriteAsync(bodyBytes, context.RequestAborted);
        }
        catch
        {
            // Only remove the in-progress marker, not a completed cached response. A client disconnect
            // after a 2xx was stored but before the body write completes must not invalidate the cache;
            // the next retry will receive a clean replay of the completed response.
            if (!completedStored)
            {
                await cache.RemoveAsync(cacheKey, CancellationToken.None);
            }
            context.Response.Body = originalBody;
            throw;
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }

    private static string BuildKey(string method, string path, string subject, string idempotencyKey)
    {
        var raw = $"{method}|{path}|{subject}|{idempotencyKey}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return $"idemp:{Convert.ToHexStringLower(hash)}";
    }

    private static async Task ReplayAsync(HttpContext context, CachedResponse stored)
    {
        context.Response.StatusCode = stored.StatusCode;
        context.Response.ContentType = stored.ContentType ?? "application/json";
        context.Response.Headers[ReplayedHeader] = "true";

        // Restore cached response headers (Content-Length/Transfer-Encoding were excluded on capture and
        // are recomputed by the response writer; headers already set above — ContentType, ReplayedHeader —
        // are skipped via ContainsKey so they are not overwritten).
        if (stored.Headers is not null)
        {
            foreach (var (name, values) in stored.Headers)
            {
                if (!context.Response.Headers.ContainsKey(name))
                {
                    context.Response.Headers[name] = values;
                }
            }
        }

        if (stored.Body is { Length: > 0 })
        {
            await context.Response.Body.WriteAsync(stored.Body, context.RequestAborted);
        }
    }

    private static async Task WriteInProgressConflictAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status409Conflict;
        var problem = new
        {
            type = "https://tools.ietf.org/html/rfc9110#section-15.5.10",
            title = "Idempotent request in progress",
            status = StatusCodes.Status409Conflict,
            detail = "A request with this Idempotency-Key is still being processed. Please retry shortly."
        };
        await context.Response.WriteAsJsonAsync(
            problem, options: null, contentType: "application/problem+json", cancellationToken: context.RequestAborted);
    }

    private static byte[] Serialize(CachedResponse value) =>
        JsonSerializer.SerializeToUtf8Bytes(value, SerializerOptions);

    private static CachedResponse? Deserialize(byte[] bytes) =>
        JsonSerializer.Deserialize<CachedResponse>(bytes, SerializerOptions);

    private sealed class CachedResponse
    {
        public bool InProgress { get; init; }
        public int StatusCode { get; init; }
        public string? ContentType { get; init; }
        public byte[]? Body { get; init; }

        /// <summary>
        /// Response headers captured on first execution, excluding hop-by-hop headers
        /// (<c>Content-Length</c>, <c>Transfer-Encoding</c>) that are recomputed on replay.
        /// </summary>
        public Dictionary<string, string[]>? Headers { get; init; }
    }
}
