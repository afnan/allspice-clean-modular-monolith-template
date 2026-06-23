using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace AllSpice.CleanModularMonolith.SharedKernel.Interceptors;

/// <summary>
/// Diagnostic interceptor that, on a <see cref="DbUpdateConcurrencyException"/>, logs the full
/// change-tracker state (entity type, state, primary key, and modified properties with their
/// original-vs-current values) so the conflicting entity and operation are easy to pinpoint.
/// Registered as a singleton and discovered by EF Core from the application service provider.
/// </summary>
public sealed class ConcurrencyDiagnosticInterceptor : SaveChangesInterceptor
{
    private readonly ILogger<ConcurrencyDiagnosticInterceptor> _logger;

    public ConcurrencyDiagnosticInterceptor(ILogger<ConcurrencyDiagnosticInterceptor> logger)
    {
        _logger = logger;
    }

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

            foreach (var prop in entry.Properties.Where(p => p.IsModified))
            {
                _logger.LogError(
                    "  Modified: {Property} = {CurrentValue} (original: {OriginalValue})",
                    prop.Metadata.Name,
                    prop.CurrentValue,
                    prop.OriginalValue);
            }
        }
    }
}
