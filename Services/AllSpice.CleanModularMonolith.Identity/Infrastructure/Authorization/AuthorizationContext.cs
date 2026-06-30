using AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;
using AllSpice.CleanModularMonolith.SharedKernel.Identity;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Authorization;

public sealed class AuthorizationContext(ICurrentUserContext currentUser) : IAuthorizationContext
{
    private readonly ICurrentUserContext _currentUser = currentUser;
    public Guid? UserId => _currentUser.LocalUserId;
    public string TenantId => Tenant.Default;
}
