using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace AllSpice.CleanModularMonolith.SharedKernel.Interceptors;

/// <summary>
/// Diagnostic interceptor that, on a <see cref="DbUpdateConcurrencyException"/>, logs the
/// change-tracker state (entity type, state, primary key, and the NAMES of the modified properties) so the
/// conflicting entity and operation are easy to pinpoint. Property values are deliberately NOT logged — they
/// can contain PII or secrets and this runs outside the <c>[SensitiveData]</c>-aware LoggingBehavior redaction.
/// Registered as a singleton and discovered by EF Core from the application service provider.
/// </summary>
public sealed class ConcurrencyDiagnosticInterceptor(ILogger<ConcurrencyDiagnosticInterceptor> logger) : SaveChangesInterceptor
{
    private readonly ILogger<ConcurrencyDiagnosticInterceptor> _logger = logger;

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        LogEntries(eventData);
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        LogEntries(eventData);
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private void LogEntries(DbContextErrorEventData eventData)
    {
        if (eventData.Exception is not DbUpdateConcurrencyException || eventData.Context is null)
        {
            return;
        }

        _logger.LogError("=== DbUpdateConcurrencyException diagnostic ({DbContext}) ===", eventData.Context.GetType().Name);

        foreach (var entry in eventData.Context.ChangeTracker.Entries())
        {
            _logger.LogError(
                "Entity: {EntityType}, State: {State}, PK: {PrimaryKey}",
                entry.Metadata.Name,
                entry.State,
                string.Join(", ", entry.Properties
                    .Where(p => p.Metadata.IsPrimaryKey())
                    .Select(p => $"{p.Metadata.Name}={p.CurrentValue}")));

            // Log the NAMES of modified properties only — never their values. Values may hold PII or secrets
            // (emails, tokens, hashes) and this interceptor bypasses the [SensitiveData] redaction applied by
            // LoggingBehavior. The property names plus the primary key above are enough to pinpoint the conflict.
            var modifiedProperties = entry.Properties
                .Where(p => p.IsModified)
                .Select(p => p.Metadata.Name)
                .ToList();

            if (modifiedProperties.Count > 0)
            {
                _logger.LogError("  Modified properties: {ModifiedProperties}", string.Join(", ", modifiedProperties));
            }
        }
    }
}
