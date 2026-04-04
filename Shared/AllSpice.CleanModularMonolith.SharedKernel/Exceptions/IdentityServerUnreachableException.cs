namespace AllSpice.CleanModularMonolith.SharedKernel.Exceptions;

/// <summary>
/// Thrown when the identity server (Keycloak) is unreachable or fails to respond.
/// </summary>
public sealed class IdentityServerUnreachableException : Exception
{
    public IdentityServerUnreachableException()
        : base("We are having trouble reaching our identity server. Please try again later.") { }

    public IdentityServerUnreachableException(string message) : base(message) { }

    public IdentityServerUnreachableException(string message, Exception innerException)
        : base(message, innerException) { }
}
