using System.Diagnostics;
using Mediator;
using Microsoft.Extensions.Logging;

namespace AllSpice.CleanModularMonolith.SharedKernel.Behaviors;

public sealed class PerformanceBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : class, IMessage
{
    private readonly ILogger<PerformanceBehavior<TRequest, TResponse>> _logger;

    public PerformanceBehavior(ILogger<PerformanceBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async ValueTask<TResponse> Handle(TRequest request, MessageHandlerDelegate<TRequest, TResponse> next, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        var response = await next(request, cancellationToken).ConfigureAwait(false);

        stopwatch.Stop();

        _logger.LogTrace("Request {RequestName} executed in {ElapsedMilliseconds} ms", typeof(TRequest).Name, stopwatch.ElapsedMilliseconds);

        return response;
    }
}


