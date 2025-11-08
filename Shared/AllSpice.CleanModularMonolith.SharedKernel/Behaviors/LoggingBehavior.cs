using Ardalis.GuardClauses;
using Mediator;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace AllSpice.CleanModularMonolith.SharedKernel.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : class, IMessage
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async ValueTask<TResponse> Handle(TRequest request, MessageHandlerDelegate<TRequest, TResponse> next, CancellationToken cancellationToken)
    {
        Guard.Against.Null(request);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Handling {RequestName}", typeof(TRequest).Name);
            var props = new List<PropertyInfo>(request.GetType().GetProperties());
            foreach (var prop in props)
            {
                var value = prop.GetValue(request, null);
                _logger.LogInformation("Property {Property} : {@Value}", prop.Name, value);
            }
        }

        var stopwatch = Stopwatch.StartNew();

        var response = await next(request, cancellationToken).ConfigureAwait(false);

        stopwatch.Stop();

        _logger.LogInformation(
            "Handled {RequestName} with {@Response} in {ElapsedMilliseconds} ms",
            typeof(TRequest).Name,
            response,
            stopwatch.ElapsedMilliseconds);

        return response;
    }
}


