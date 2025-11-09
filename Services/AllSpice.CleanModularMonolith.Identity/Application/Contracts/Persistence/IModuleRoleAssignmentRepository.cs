using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.ModuleRoleAssignment;
using AllSpice.CleanModularMonolith.Identity.Domain.ValueObjects;
using AllSpice.CleanModularMonolith.SharedKernel.Repositories;

namespace AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;

public interface IModuleRoleAssignmentRepository : IRepository<ModuleRoleAssignment>, IReadRepository<ModuleRoleAssignment>
{
    Task<IReadOnlyCollection<ModuleRoleAssignment>> GetActiveAssignmentsAsync(ExternalUserId userId, CancellationToken cancellationToken = default);
    Task<ModuleRoleAssignment?> FindAssignmentAsync(ExternalUserId userId, string moduleKey, string roleKey, CancellationToken cancellationToken = default);
}


