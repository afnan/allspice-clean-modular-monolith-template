using System.Text.Json;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Entities;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Extensions;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Options;
using AllSpice.CleanModularMonolith.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace AllSpice.CleanModularMonolith.Identity.Infrastructure.Jobs;

/// <summary>
/// Reconciles Authentik users with local module-role assignments to detect orphaned accounts.
/// </summary>
[DisallowConcurrentExecution]
public sealed class AuthentikUserSyncJob : IJob
{
    public const string JobIdentity = "AuthentikUserSyncJob";
    private const string IssueType = "AuthentikUserSync";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<IdentitySyncOptions> _options;
    private readonly ILogger<AuthentikUserSyncJob> _logger;

    public AuthentikUserSyncJob(
        IHttpClientFactory httpClientFactory,
        IServiceScopeFactory scopeFactory,
        IOptions<IdentitySyncOptions> options,
        ILogger<AuthentikUserSyncJob> logger)
    {
        _httpClientFactory = httpClientFactory;
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var history = new IdentitySyncHistory
        {
            JobName = JobIdentity,
            StartedUtc = DateTimeOffset.UtcNow,
            CorrelationId = Guid.NewGuid().ToString("N")
        };

        try
        {
            var cancellationToken = context.CancellationToken;
            var httpClient = _httpClientFactory.CreateClient(IdentityModuleExtensions.AuthentikHttpClientName);
            var assignments = await dbContext.ModuleRoleAssignments
                .Where(a => a.RevokedUtc == null)
                .Select(a => a.UserId.Value)
                .ToListAsync(cancellationToken);

            var activeAssignmentSet = new HashSet<string>(assignments, StringComparer.OrdinalIgnoreCase);
            var orphanCandidates = new Dictionary<string, OrphanCandidate>(StringComparer.OrdinalIgnoreCase);

            var pageSize = Math.Max(1, _options.Value.PageSize);
            var next = $"/api/v3/core/users/?ordering=pk&limit={pageSize}";

            while (!string.IsNullOrEmpty(next))
            {
                using var response = await httpClient.GetAsync(next, cancellationToken);
                response.EnsureSuccessStatusCode();

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                if (document.RootElement.TryGetProperty("results", out var results))
                {
                    foreach (var element in results.EnumerateArray())
                    {
                        history.ProcessedCount++;

                        var uuid = element.TryGetProperty("uuid", out var uuidElement)
                            ? uuidElement.GetString()
                            : element.TryGetProperty("pk", out var pkElement)
                                ? pkElement.GetRawText()
                                : null;

                        if (string.IsNullOrWhiteSpace(uuid))
                        {
                            continue;
                        }

                        if (activeAssignmentSet.Contains(uuid))
                        {
                            continue;
                        }

                        orphanCandidates[uuid] = new OrphanCandidate(
                            uuid,
                            element.TryGetProperty("username", out var usernameElement) ? usernameElement.GetString() : null,
                            element.TryGetProperty("email", out var emailElement) ? emailElement.GetString() : null,
                            element.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null);
                    }
                }

                next = document.RootElement.TryGetProperty("next", out var nextElement) && nextElement.ValueKind != JsonValueKind.Null
                    ? nextElement.GetString()
                    : null;
            }

            history.OrphanCount = orphanCandidates.Count;
            history.Succeeded = true;
            history.FinishedUtc = DateTimeOffset.UtcNow;

            await PersistOrphansAsync(dbContext, orphanCandidates.Values, cancellationToken);
            await ResolveSyncIssuesAsync(dbContext, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            history.Succeeded = false;
            history.FinishedUtc = DateTimeOffset.UtcNow;
            history.ErrorMessage = ex.Message;

            await using var failureScope = _scopeFactory.CreateAsyncScope();
            var failureContext = failureScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            await RecordSyncIssueAsync(failureContext, ex, context.CancellationToken);

            _logger.LogError(ex, "Authentik user sync job failed with correlation id {CorrelationId}", history.CorrelationId);

            throw new JobExecutionException(ex, false);
        }
        finally
        {
            history.FinishedUtc = history.FinishedUtc == default ? DateTimeOffset.UtcNow : history.FinishedUtc;

            await using var finalScope = _scopeFactory.CreateAsyncScope();
            var finalContext = finalScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            finalContext.IdentitySyncHistories.Add(history);
            await finalContext.SaveChangesAsync(context.CancellationToken);
        }
    }

    private async Task PersistOrphansAsync(
        IdentityDbContext dbContext,
        IEnumerable<OrphanCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var candidateMap = candidates.ToDictionary(c => c.UserId, StringComparer.OrdinalIgnoreCase);

        var existing = await dbContext.IdentityOrphanUsers.ToListAsync(cancellationToken);

        foreach (var orphan in existing)
        {
            if (candidateMap.TryGetValue(orphan.UserId, out var candidate))
            {
                orphan.Username = candidate.Username;
                orphan.Email = candidate.Email;
                orphan.DisplayName = candidate.DisplayName;
                orphan.LastDetectedUtc = now;
                orphan.ResolvedUtc = null;

                candidateMap.Remove(orphan.UserId);
            }
            else if (orphan.ResolvedUtc is null)
            {
                orphan.ResolvedUtc = now;
                orphan.LastDetectedUtc = now;
            }
        }

        foreach (var candidate in candidateMap.Values)
        {
            dbContext.IdentityOrphanUsers.Add(new IdentityOrphanUser
            {
                UserId = candidate.UserId,
                Username = candidate.Username,
                Email = candidate.Email,
                DisplayName = candidate.DisplayName,
                FirstDetectedUtc = now,
                LastDetectedUtc = now
            });
        }

        // Defer SaveChanges to caller so multiple operations can be batched.
    }

    private async Task ResolveSyncIssuesAsync(IdentityDbContext dbContext, CancellationToken cancellationToken)
    {
        var unresolved = await dbContext.IdentitySyncIssues
            .Where(issue => issue.IssueType == IssueType && issue.ResolvedUtc == null)
            .ToListAsync(cancellationToken);

        if (unresolved.Count == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var issue in unresolved)
        {
            issue.ResolvedUtc = now;
        }

        // Defer SaveChanges to caller so multiple operations can be batched.
    }

    private async Task RecordSyncIssueAsync(IdentityDbContext dbContext, Exception ex, CancellationToken cancellationToken)
    {
        var issue = await dbContext.IdentitySyncIssues
            .Where(i => i.IssueType == IssueType && i.ResolvedUtc == null)
            .FirstOrDefaultAsync(cancellationToken);

        if (issue is null)
        {
            issue = new IdentitySyncIssue
            {
                IssueType = IssueType,
                Message = ex.Message,
                Details = ex.ToString(),
                CreatedUtc = DateTimeOffset.UtcNow,
                LastOccurredUtc = DateTimeOffset.UtcNow
            };

            dbContext.IdentitySyncIssues.Add(issue);
        }
        else
        {
            issue.Message = ex.Message;
            issue.Details = ex.ToString();
            issue.LastOccurredUtc = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private sealed record OrphanCandidate(string UserId, string? Username, string? Email, string? DisplayName);
}


