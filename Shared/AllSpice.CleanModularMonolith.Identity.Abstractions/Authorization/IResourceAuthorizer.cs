using Ardalis.Result;

namespace AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;

/// <summary>Evaluates resource/ownership rules for a loaded aggregate. Keeps HttpContext out of handlers.</summary>
public interface IResourceAuthorizer
{
    Task<Result> AuthorizeAsync<TResource>(TResource resource, string action, CancellationToken cancellationToken)
        where TResource : notnull;
}
