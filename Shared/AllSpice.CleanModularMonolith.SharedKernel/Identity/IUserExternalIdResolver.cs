namespace AllSpice.CleanModularMonolith.SharedKernel.Identity;

public interface IUserExternalIdResolver
{
    Task<string?> GetExternalIdByLocalIdAsync(Guid localUserId, CancellationToken cancellationToken = default);
}
