using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Invitation;
using AllSpice.CleanModularMonolith.SharedKernel.Repositories;

namespace AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;

/// <summary>
/// Bespoke repository for the Invitation aggregate. Inherits the standard
/// IRepository / IReadRepository surfaces (Add/Update/Delete/ListAsync/...) from
/// Ardalis.Specification and adds query methods that are awkward to express as
/// specifications.
/// </summary>
public interface IInvitationRepository : IRepository<Invitation>, IReadRepository<Invitation>
{
    Task<Invitation?> GetByTokenAsync(Guid token, CancellationToken cancellationToken = default);
    Task<Invitation?> GetPendingByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Invitation>> ListAllAsync(CancellationToken cancellationToken = default);
}
