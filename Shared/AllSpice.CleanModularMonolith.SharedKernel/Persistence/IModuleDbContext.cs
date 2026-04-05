using Microsoft.EntityFrameworkCore;

namespace AllSpice.CleanModularMonolith.SharedKernel.Persistence;

/// <summary>
/// Abstraction that allows the TransactionBehavior to access a module's DbContext
/// without coupling to a specific implementation.
/// </summary>
public interface IModuleDbContext
{
    DbContext Instance { get; }
}
