using FluentValidation;
using Mediator;
using Microsoft.Extensions.Logging;
using AllSpice.CleanModularMonolith.SharedKernel.Exceptions;
using AllSpice.CleanModularMonolith.SharedKernel.Results;

namespace AllSpice.CleanModularMonolith.SharedKernel.Behaviors;

/// <summary>
/// Pipeline behavior that catches domain and validation exceptions and maps them to Ardalis Result types.
/// This allows handlers to throw typed domain exceptions instead of returning Result.Error manually.
/// </summary>
public sealed class DomainExceptionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : class, IMessage
{
    private readonly ILogger<DomainExceptionBehavior<TRequest, TResponse>> _logger;

    public DomainExceptionBehavior(ILogger<DomainExceptionBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async ValueTask<TResponse> Handle(TRequest request, MessageHandlerDelegate<TRequest, TResponse> next, CancellationToken cancellationToken)
    {
        try
        {
            return await next(request, cancellationToken).ConfigureAwait(false);
        }
        catch (DomainException ex)
        {
            _logger.LogWarning(ex, "Domain exception in {RequestType}: {Message}", typeof(TRequest).Name, ex.Message);
            return DomainExceptionResultMapper.MapToResult<TResponse>(ex);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning(ex, "Validation exception in {RequestType}: {Message}", typeof(TRequest).Name, ex.Message);
            return DomainExceptionResultMapper.MapToResult<TResponse>(ex);
        }
    }
}
