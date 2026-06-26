namespace AllSpice.CleanModularMonolith.SharedKernel.Messaging;

/// <summary>
/// Releases the integration-event envelopes that a command persisted into the durable outbox during its
/// transaction, so they are sent immediately after the commit instead of waiting for the messaging
/// infrastructure's background recovery sweep (seconds of latency).
/// <para>
/// SharedKernel stays messaging-agnostic: the gateway registers the concrete (Wolverine-backed)
/// implementation. <see cref="Behaviors.TransactionBehavior{TRequest, TResponse}"/> injects the set of
/// registered flushers and invokes them once, after a successful commit. When none is registered the
/// behavior is unchanged — envelopes are still delivered, just by the durable recovery loop.
/// </para>
/// </summary>
public interface IOutboxFlusher
{
    /// <summary>
    /// Sends the outbox messages buffered in the current unit of work. Best-effort: the caller treats a
    /// failure as non-fatal because the envelope is already durably persisted (the recovery loop will still
    /// deliver it), so a flush failure must not fail an already-committed command.
    /// </summary>
    ValueTask FlushAsync(CancellationToken cancellationToken = default);
}
