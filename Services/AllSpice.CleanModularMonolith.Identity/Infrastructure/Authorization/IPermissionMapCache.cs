using AllSpice.CleanModularMonolith.Identity.Application.Contracts.Authorization;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Authorization;

public interface IPermissionMapCache
{
    ValueTask<PermissionMap> GetAsync(CancellationToken cancellationToken);
    void Invalidate();
}
