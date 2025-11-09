using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.ModuleDefinition;
using AllSpice.CleanModularMonolith.SharedKernel.Repositories;

namespace AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;

public interface IModuleDefinitionRepository : IRepository<ModuleDefinition>, IReadRepository<ModuleDefinition>
{
    Task<ModuleDefinition?> GetByKeyAsync(string moduleKey, CancellationToken cancellationToken = default);
}


