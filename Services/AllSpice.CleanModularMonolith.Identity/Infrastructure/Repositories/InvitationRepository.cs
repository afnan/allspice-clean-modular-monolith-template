using Ardalis.Specification.EntityFrameworkCore;
using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Persistence;
using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Invitation;
using AllSpice.CleanModularMonolith.Identity.Domain.Enums;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Repositories;

public sealed class InvitationRepository : RepositoryBase<Invitation>, IInvitationRepository
{
    private readonly IdentityDbContext _dbContext;

    public InvitationRepository(IdentityDbContext dbContext)
        : base(dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Invitation?> GetByTokenAsync(Guid token, CancellationToken cancellationToken = default) =>
        _dbContext.Invitations
            .FirstOrDefaultAsync(i => i.Token == token, cancellationToken);

    public Task<Invitation?> GetPendingByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        _dbContext.Invitations
            .FirstOrDefaultAsync(i => EF.Functions.ILike(i.Email, email) && i.Status == InvitationStatus.Pending, cancellationToken);

    public async Task<IReadOnlyList<Invitation>> ListAllAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.Invitations
            .OrderByDescending(i => i.CreatedOnUtc)
            .ToListAsync(cancellationToken);
}
