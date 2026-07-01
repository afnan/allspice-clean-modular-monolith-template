namespace AllSpice.CleanModularMonolith.Identity.Abstractions.Authorization;

/// <summary>Single-tenant seam. Real tenant resolution slots in later without changing rule signatures.</summary>
public static class Tenant
{
    public const string Default = "default";
}
