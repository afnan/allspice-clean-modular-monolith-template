namespace AllSpice.CleanModularMonolith.SharedKernel.Auditing;

public interface IAuditable
{
    DateTimeOffset CreatedOnUtc { get; }
    string? CreatedBy { get; }
    DateTimeOffset? LastModifiedOnUtc { get; }
    string? LastModifiedBy { get; }

    void SetCreated(string? userId);
    void SetModified(string? userId);
}


