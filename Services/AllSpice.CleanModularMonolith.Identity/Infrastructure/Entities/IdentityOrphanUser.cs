namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Entities;

/// <summary>
/// Represents a user present in Authentik without any active module role assignments.
/// </summary>
public sealed class IdentityOrphanUser
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string UserId { get; set; } = string.Empty;

    public string? Username { get; set; }

    public string? Email { get; set; }

    public string? DisplayName { get; set; }

    public DateTimeOffset FirstDetectedUtc { get; set; }

    public DateTimeOffset LastDetectedUtc { get; set; }

    public DateTimeOffset? ResolvedUtc { get; set; }
}


