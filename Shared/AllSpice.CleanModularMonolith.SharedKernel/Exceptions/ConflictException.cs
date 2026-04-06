namespace AllSpice.CleanModularMonolith.SharedKernel.Exceptions;

/// <summary>
/// Thrown when a create or update would violate a unique constraint.
/// Mapped to <see cref="Ardalis.Result.ResultStatus.Conflict"/> (409) by the Mediator pipeline.
/// </summary>
public sealed class ConflictException : DomainException
{
    public ConflictException(string entityName, object key)
        : base($"{entityName} with key '{key}' already exists.")
    {
        EntityName = entityName;
        Key = key;
    }

    public string EntityName { get; }
    public object Key { get; }
}
