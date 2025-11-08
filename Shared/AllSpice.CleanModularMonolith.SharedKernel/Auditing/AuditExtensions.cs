namespace AllSpice.CleanModularMonolith.SharedKernel.Auditing;

public static class AuditExtensions
{
    public static void StampCreated(this IAuditable auditable, string? userId)
    {
        auditable.SetCreated(userId);
    }

    public static void StampModified(this IAuditable auditable, string? userId)
    {
        auditable.SetModified(userId);
    }
}


