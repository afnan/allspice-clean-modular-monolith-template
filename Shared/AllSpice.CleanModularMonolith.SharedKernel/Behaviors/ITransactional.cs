namespace AllSpice.CleanModularMonolith.SharedKernel.Behaviors;

/// <summary>
/// Marker interface for commands that should be wrapped in a database transaction
/// by <see cref="TransactionBehavior{TRequest, TResponse}"/>.
/// Queries should NOT implement this interface.
/// </summary>
public interface ITransactional;
