using Ardalis.Result;
using FluentValidation;
using AllSpice.CleanModularMonolith.SharedKernel.Exceptions;

namespace AllSpice.CleanModularMonolith.SharedKernel.Results;

/// <summary>
/// Maps domain and validation exceptions to Ardalis Result types with appropriate status codes.
/// Used by DomainExceptionBehavior in the Mediator pipeline so handlers can throw domain exceptions
/// and the pipeline converts them to the appropriate Result status.
/// </summary>
public static class DomainExceptionResultMapper
{
    public static TResponse MapToResult<TResponse>(Exception exception)
    {
        var responseType = typeof(TResponse);

        if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var innerType = responseType.GetGenericArguments()[0];
            var method = typeof(DomainExceptionResultMapper)
                .GetMethod(nameof(MapToTypedResult), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .MakeGenericMethod(innerType);
            return (TResponse)method.Invoke(null, [exception])!;
        }

        if (responseType == typeof(Result))
        {
            return (TResponse)(object)MapToUntypedResult(exception);
        }

        throw new InvalidOperationException(
            $"DomainExceptionBehavior cannot map exceptions for response type {responseType.Name}. " +
            "Only Ardalis.Result and Ardalis.Result<T> are supported.");
    }

    private static Result MapToUntypedResult(Exception exception) =>
        exception switch
        {
            NotFoundException ex => Result.NotFound(ex.Message),
            DomainValidationException ex => Result.Invalid(ToValidationErrors(ex)),
            UnauthorizedException => Result.Unauthorized(),
            ConflictException ex => Result.Conflict(ex.Message),
            ForbiddenException => Result.Forbidden(),
            BusinessRuleViolationException ex => Result.Error(ex.Message),
            DomainException ex => Result.Error(ex.Message),
            ValidationException ex => Result.Invalid(ToValidationErrors(ex)),
            _ => throw exception
        };

    private static Result<T> MapToTypedResult<T>(Exception exception) =>
        exception switch
        {
            NotFoundException ex => Result<T>.NotFound(ex.Message),
            DomainValidationException ex => Result<T>.Invalid(ToValidationErrors(ex)),
            UnauthorizedException => Result<T>.Unauthorized(),
            ConflictException ex => Result<T>.Conflict(ex.Message),
            ForbiddenException => Result<T>.Forbidden(),
            BusinessRuleViolationException ex => Result<T>.Error(ex.Message),
            DomainException ex => Result<T>.Error(ex.Message),
            ValidationException ex => Result<T>.Invalid(ToValidationErrors(ex)),
            _ => throw exception
        };

    private static List<ValidationError> ToValidationErrors(DomainValidationException ex)
    {
        if (ex.Errors.Count > 0)
        {
            return ex.Errors
                .SelectMany(kvp => kvp.Value.Select(msg => new ValidationError
                {
                    Identifier = kvp.Key,
                    ErrorMessage = msg
                }))
                .ToList();
        }

        return [new ValidationError { ErrorMessage = ex.Message }];
    }

    private static List<ValidationError> ToValidationErrors(ValidationException ex) =>
        ex.Errors
            .Select(e => new ValidationError
            {
                Identifier = e.PropertyName,
                ErrorMessage = e.ErrorMessage
            })
            .ToList();
}
