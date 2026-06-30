using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using AllSpice.CleanModularMonolith.SharedKernel.Identity;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Authorization;

public sealed class AuthorizationContext(ICurrentUserContext currentUser) : IAuthorizationContext
{
    public Guid? UserId => currentUser.LocalUserId;
    public string TenantId => Tenant.Default;
}
