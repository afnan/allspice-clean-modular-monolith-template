namespace AllSpice.CleanModularMonolith.SharedKernel.Exceptions;

/// <summary>
/// Thrown when a domain rule is violated.
/// Mapped to <see cref="Ardalis.Result.ResultStatus.Error"/> (400) by the Mediator pipeline.
/// </summary>
public class BusinessRuleViolationException : DomainException
{
    public BusinessRuleViolationException(string message) : base(message) { }
}
