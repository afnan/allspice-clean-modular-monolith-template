namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Entities;

/// <summary>
/// Records the outcome of a synchronization job execution.
/// </summary>
public sealed class IdentitySyncHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string JobName { get; set; } = string.Empty;

    public DateTimeOffset StartedUtc { get; set; }

    public DateTimeOffset FinishedUtc { get; set; }

    public bool Succeeded { get; set; }

    public string? ErrorMessage { get; set; }

    public int ProcessedCount { get; set; }

    public int OrphanCount { get; set; }

    public string? CorrelationId { get; set; }
}


