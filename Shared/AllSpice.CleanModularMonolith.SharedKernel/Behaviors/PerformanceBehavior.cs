using System.Diagnostics;
using Mediator;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AllSpice.CleanModularMonolith.SharedKernel.Behaviors;

public sealed class PerformanceBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : class, IMessage
{
    private readonly ILogger<PerformanceBehavior<TRequest, TResponse>> _logger;
    private readonly long _slowRequestThresholdMs;

    public PerformanceBehavior(
        ILogger<PerformanceBehavior<TRequest, TResponse>> logger,
        IOptions<PerformanceBehaviorOptions> options)
    {
        _logger = logger;
        _slowRequestThresholdMs = options.Value.SlowRequestThresholdMs;
    }

    public async ValueTask<TResponse> Handle(TRequest request, MessageHandlerDelegate<TRequest, TResponse> next, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        var response = await next(request, cancellationToken).ConfigureAwait(false);

        stopwatch.Stop();

        var elapsedMs = stopwatch.ElapsedMilliseconds;
        if (elapsedMs >= _slowRequestThresholdMs)
        {
            _logger.LogWarning(
                "Slow request {RequestName} executed in {ElapsedMilliseconds} ms (threshold {ThresholdMs} ms)",
                typeof(TRequest).Name, elapsedMs, _slowRequestThresholdMs);
        }
        else
        {
            _logger.LogTrace("Request {RequestName} executed in {ElapsedMilliseconds} ms", typeof(TRequest).Name, elapsedMs);
        }

        return response;
    }
}


