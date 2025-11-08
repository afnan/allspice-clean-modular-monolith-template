namespace AllSpice.CleanModularMonolith.SharedKernel.Exceptions;

public sealed class DomainValidationException : DomainException
{
    public DomainValidationException(string message)
        : base(message)
    {
    }

    public DomainValidationException(string message, IDictionary<string, string[]> errors)
        : base(message)
    {
        Errors = errors;
    }

    public IDictionary<string, string[]> Errors { get; } = new Dictionary<string, string[]>();
}


