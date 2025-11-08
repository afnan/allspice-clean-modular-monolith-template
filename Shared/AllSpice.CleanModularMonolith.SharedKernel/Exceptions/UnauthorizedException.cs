namespace AllSpice.CleanModularMonolith.SharedKernel.Exceptions;

public sealed class UnauthorizedException : DomainException
{
    public UnauthorizedException(string message = "You are not authorized to execute this action.")
        : base(message)
    {
    }
}


