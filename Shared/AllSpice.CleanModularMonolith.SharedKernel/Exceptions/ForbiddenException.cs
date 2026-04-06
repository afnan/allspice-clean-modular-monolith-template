namespace AllSpice.CleanModularMonolith.SharedKernel.Exceptions;

/// <summary>
/// Thrown when the user is authenticated but not allowed to perform the action.
/// Mapped to <see cref="Ardalis.Result.ResultStatus.Forbidden"/> (403) by the Mediator pipeline.
/// </summary>
public sealed class ForbiddenException : DomainException
{
    public ForbiddenException(string message = "You do not have permission to perform this action.")
        : base(message) { }
}
