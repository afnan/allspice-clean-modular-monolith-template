namespace AllSpice.CleanModularMonolith.SharedKernel.Auditing;

public interface ISoftDelete
{
    bool IsDeleted { get; }
    DateTimeOffset? DeletedOnUtc { get; }
    string? DeletedBy { get; }

    void MarkDeleted(string? userId);
    void Restore();
}


