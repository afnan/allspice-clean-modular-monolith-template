using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;

/// <summary>Materializes a one-requirement policy for any <c>perm:{key}</c> name; delegates everything else
/// (incl. <c>authenticated</c> / <c>allow-anonymous</c> / fallback) to the default provider.</summary>
public sealed class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
        => _fallback = new DefaultAuthorizationPolicyProvider(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (PermissionPolicy.TryGetKey(policyName, out var key))
        {
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(key))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return _fallback.GetPolicyAsync(policyName);
    }
}
