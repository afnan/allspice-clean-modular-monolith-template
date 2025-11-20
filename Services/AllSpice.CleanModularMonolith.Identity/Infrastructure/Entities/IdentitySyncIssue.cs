namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Entities;

/// <summary>
/// Represents an outstanding synchronization issue that should be surfaced to administrators.
/// </summary>
public sealed class IdentitySyncIssue
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string IssueType { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? Details { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset LastOccurredUtc { get; set; }

    public DateTimeOffset? ResolvedUtc { get; set; }
}


