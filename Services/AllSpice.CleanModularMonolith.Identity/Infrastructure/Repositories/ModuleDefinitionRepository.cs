using Ardalis.Specification.EntityFrameworkCore;
using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.ModuleDefinition;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Repositories;

public sealed class ModuleDefinitionRepository : RepositoryBase<ModuleDefinition>, IModuleDefinitionRepository
{
    private readonly IdentityDbContext _dbContext;

    public ModuleDefinitionRepository(IdentityDbContext dbContext)
        : base(dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<ModuleDefinition?> GetByKeyAsync(string moduleKey, CancellationToken cancellationToken = default) =>
        _dbContext.ModuleDefinitions
            .Include(module => module.Roles)
            .FirstOrDefaultAsync(module => module.Key == moduleKey, cancellationToken);
}


