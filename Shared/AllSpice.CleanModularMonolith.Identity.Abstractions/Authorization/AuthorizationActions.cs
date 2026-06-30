using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;

/// <summary>Canonical resource-operation names; reused as <see cref="OperationAuthorizationRequirement.Name"/>.</summary>
public static class AuthorizationActions
{
    public const string Read = "read";
    public const string Create = "create";
    public const string Update = "update";
    public const string Delete = "delete";
    public const string Approve = "approve";
}
