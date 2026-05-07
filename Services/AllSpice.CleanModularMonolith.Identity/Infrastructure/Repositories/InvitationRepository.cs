using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Invitation;
using AllSpice.CleanModularMonolith.Identity.Domain.Enums;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Repositories;

public sealed class InvitationRepository : IInvitationRepository
{
    private readonly IdentityDbContext _dbContext;

    public InvitationRepository(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Invitation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _dbContext.Invitations
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public Task<Invitation?> GetByTokenAsync(Guid token, CancellationToken cancellationToken = default) =>
        _dbContext.Invitations
            .FirstOrDefaultAsync(i => i.Token == token, cancellationToken);

    public Task<Invitation?> GetPendingByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        _dbContext.Invitations
            .FirstOrDefaultAsync(i => EF.Functions.ILike(i.Email, email) && i.Status == InvitationStatus.Pending, cancellationToken);

    public async Task<List<Invitation>> ListAllAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.Invitations
            .OrderByDescending(i => i.CreatedOnUtc)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Invitation invitation, CancellationToken cancellationToken = default)
    {
        await _dbContext.Invitations.AddAsync(invitation, cancellationToken);
    }

    public Task UpdateAsync(Invitation invitation, CancellationToken cancellationToken = default)
    {
        _dbContext.Invitations.Update(invitation);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _dbContext.SaveChangesAsync(cancellationToken);
}
