using Ardalis.GuardClauses;
using Mediator;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace AllSpice.CleanModularMonolith.SharedKernel.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : class, IMessage
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger = logger;

    public async ValueTask<TResponse> Handle(TRequest request, MessageHandlerDelegate<TRequest, TResponse> next, CancellationToken cancellationToken)
    {
        Guard.Against.Null(request);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Handling {RequestName}", typeof(TRequest).Name);
            foreach (var prop in request.GetType().GetProperties())
            {
                // Redact anything marked [SensitiveData] (passwords/tokens/PII) so it never reaches the log sink.
                if (prop.IsDefined(typeof(SensitiveDataAttribute), inherit: true))
                {
                    _logger.LogInformation("Property {Property} : ***", prop.Name);
                    continue;
                }

                var value = prop.GetValue(request, null);
                _logger.LogInformation("Property {Property} : {@Value}", prop.Name, value);
            }
        }

        var stopwatch = Stopwatch.StartNew();

        var response = await next(request, cancellationToken).ConfigureAwait(false);

        stopwatch.Stop();

        // Log only the request name + elapsed — never the response payload, which may carry PII or secrets
        // that no [SensitiveData] annotation can reach (the attribute applies to request properties).
        _logger.LogInformation(
            "Handled {RequestName} in {ElapsedMilliseconds} ms",
            typeof(TRequest).Name,
            stopwatch.ElapsedMilliseconds);

        return response;
    }
}


