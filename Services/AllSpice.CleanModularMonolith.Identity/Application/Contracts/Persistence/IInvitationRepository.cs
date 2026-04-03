using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Invitation;

namespace AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;

public interface IInvitationRepository
{
    Task<Invitation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Invitation?> GetByTokenAsync(Guid token, CancellationToken cancellationToken = default);
    Task<Invitation?> GetPendingByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<List<Invitation>> ListAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Invitation invitation, CancellationToken cancellationToken = default);
    Task UpdateAsync(Invitation invitation, CancellationToken cancellationToken = default);
}
