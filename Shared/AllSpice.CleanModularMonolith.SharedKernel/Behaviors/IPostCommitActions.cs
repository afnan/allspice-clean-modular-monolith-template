namespace AllSpice.CleanModularMonolith.SharedKernel.Behaviors;

/// <summary>
/// Scoped collector of best-effort side effects that must run only AFTER an <see cref="ITransactional"/>
/// command's transaction has committed — e.g. a cache-eviction nudge that would otherwise publish a stale
/// read if fired from inside the handler (before the write is durable). Handlers enqueue; the
/// <see cref="TransactionBehavior{TRequest, TResponse}"/> drains and runs them post-commit.
/// </summary>
public interface IPostCommitActions
{
    /// <summary>Queue an action to run once the current command's transaction has committed.</summary>
    void Enqueue(Func<CancellationToken, Task> action);

    /// <summary>Returns the queued actions and clears the queue.</summary>
    IReadOnlyList<Func<CancellationToken, Task>> Drain();
}

/// <inheritdoc />
public sealed class PostCommitActions : IPostCommitActions
{
    private readonly List<Func<CancellationToken, Task>> _actions = [];

    public void Enqueue(Func<CancellationToken, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _actions.Add(action);
    }

    public IReadOnlyList<Func<CancellationToken, Task>> Drain()
    {
        if (_actions.Count == 0)
        {
            return [];
        }

        var snapshot = _actions.ToArray();
        _actions.Clear();
        return snapshot;
    }
}
