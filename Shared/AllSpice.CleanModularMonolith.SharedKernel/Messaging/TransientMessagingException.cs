namespace AllSpice.CleanModularMonolith.SharedKernel.Messaging;

/// <summary>
/// Marks an exception as a transient messaging failure that Wolverine should retry
/// (database hiccup, downstream service warming up, intermittent network blip).
/// Throw this from a Wolverine handler to opt into the retry policy registered in
/// the gateway. Anything else escapes to the error queue / dead-letter.
/// </summary>
/// <remarks>
/// The previous code path threw <see cref="InvalidOperationException"/> with a string
/// message when an integration-event handler couldn't process a message — but the
/// gateway also has a global retry on <see cref="InvalidOperationException"/>, so
/// genuine programming bugs would loop forever instead of dead-lettering. Use this
/// type to express "I know this is transient and retrying is safe."
/// </remarks>
public sealed class TransientMessagingException : Exception
{
    public TransientMessagingException(string message) : base(message)
    {
    }

    public TransientMessagingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
