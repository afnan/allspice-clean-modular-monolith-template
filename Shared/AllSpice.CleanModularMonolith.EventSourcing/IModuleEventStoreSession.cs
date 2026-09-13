using AllSpice.CleanModularMonolith.SharedKernel.EventSourcing;
using Marten;

namespace AllSpice.CleanModularMonolith.EventSourcing;

/// <summary>
/// What a module's event-sourced repositories need from the transaction participant: a write session that
/// is enlisted in the module transaction, a throw-away query session for reads, and a way to register the
/// aggregates whose events must be dispatched and cleared. Typed by the module's store marker so two modules'
/// stores can coexist in one scope.
/// </summary>
/// <typeparam name="TStore">The module's <see cref="IDocumentStore"/> marker interface.</typeparam>
public interface IModuleEventStoreSession<TStore>
    where TStore : IDocumentStore
{
    /// <summary>
    /// Returns the scope's write session, opening the module transaction and the session on first use.
    /// Declares write intent — never call it from a query handler; use <see cref="OpenQuerySession"/>.
    /// </summary>
    ValueTask<IDocumentSession> GetSessionAsync(CancellationToken cancellationToken);

    /// <summary>Opens a read-only session on its own connection (no transaction). Dispose it.</summary>
    IQuerySession OpenQuerySession();

    /// <summary>Registers an aggregate so its domain events are dispatched and its events cleared after flush.</summary>
    void Track(IEventSourcedAggregate aggregate);

    /// <summary>Signals staged work (append/start/archive) that the next flush must persist.</summary>
    void MarkPending();
}
