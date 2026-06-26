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

        var subject = context.User.Identity?.IsAuthenticated == true
            ? context.User.Identity!.Name ?? "anonymous"
            : "anonymous";
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
        await cache.SetAsync(
            cacheKey,
            Serialize(new CachedResponse { InProgress = true }),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = InProgressTtl },
            context.RequestAborted);

        var originalBody = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await next(context);

            var bodyBytes = buffer.ToArray();

            if (context.Response.StatusCode is >= 200 and < 300)
            {
                var stored = new CachedResponse
                {
                    StatusCode = context.Response.StatusCode,
                    ContentType = context.Response.ContentType,
                    Body = bodyBytes,
                    InProgress = false
                };
                await cache.SetAsync(
                    cacheKey,
                    Serialize(stored),
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CompletedTtl },
                    context.RequestAborted);
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
            // Release the in-flight marker so the failed command can be retried.
            await cache.RemoveAsync(cacheKey, CancellationToken.None);
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
    }
}
