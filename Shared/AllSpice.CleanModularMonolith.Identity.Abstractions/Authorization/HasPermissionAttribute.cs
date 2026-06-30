using Microsoft.AspNetCore.Authorization;

namespace AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;

/// <summary>Sugar for MVC-style endpoints: <c>[HasPermission("cms:articles.publish")]</c>.
/// FastEndpoints use <c>Policies(PermissionPolicy.For("..."))</c> in <c>Configure()</c>.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(string permissionKey) => Policy = PermissionPolicy.For(permissionKey);
}
