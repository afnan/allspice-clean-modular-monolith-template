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
/// Reconciles Keycloak users with local <c>Users</c> records to detect orphaned accounts —
/// Keycloak users that have no corresponding local user row.
/// </summary>
[DisallowConcurrentExecution]
public sealed class KeycloakUserSyncJob : IJob
{
    public const string JobIdentity = "KeycloakUserSyncJob";
    private const string IssueType = "KeycloakUserSync";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<IdentitySyncOptions> _options;
    private readonly ILogger<KeycloakUserSyncJob> _logger;

    public KeycloakUserSyncJob(
        IHttpClientFactory httpClientFactory,
        IServiceScopeFactory scopeFactory,
        IOptions<IdentitySyncOptions> options,
        ILogger<KeycloakUserSyncJob> logger)
    {
        _httpClientFactory = httpClientFactory;
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var cancellationToken = context.CancellationToken;

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
            var localUserExternalIds = await dbContext.Users
                .Select(u => u.ExternalId.Value)
                .ToListAsync(cancellationToken);

            var knownExternalIds = new HashSet<string>(localUserExternalIds, StringComparer.OrdinalIgnoreCase);
            var orphanCandidates = new Dictionary<string, OrphanCandidate>(StringComparer.OrdinalIgnoreCase);

            await EnumerateKeycloakUsersAsync(orphanCandidates, knownExternalIds, history, cancellationToken);

            history.OrphanCount = orphanCandidates.Count;

            await PersistOrphansAsync(dbContext, orphanCandidates.Values, cancellationToken);
            await ResolveSyncIssuesAsync(dbContext, cancellationToken);

            history.Succeeded = true;
            history.FinishedUtc = DateTimeOffset.UtcNow;
            dbContext.IdentitySyncHistories.Add(history);

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            history.Succeeded = false;
            history.ErrorMessage = ex.Message;
            history.FinishedUtc = DateTimeOffset.UtcNow;

            // Discard any uncommitted partial work so the failure record persists cleanly
            // alongside the issue row in a single SaveChanges.
            dbContext.ChangeTracker.Clear();

            await RecordSyncIssueAsync(dbContext, ex, cancellationToken);
            dbContext.IdentitySyncHistories.Add(history);
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogError(ex, "Keycloak user sync job failed with correlation id {CorrelationId}", history.CorrelationId);
            throw new JobExecutionException(ex, false);
        }
    }

    private async Task EnumerateKeycloakUsersAsync(
        Dictionary<string, OrphanCandidate> orphanCandidates,
        HashSet<string> knownExternalIds,
        IdentitySyncHistory history,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient(IdentityModuleExtensions.KeycloakHttpClientName);
        var pageSize = Math.Max(1, _options.Value.PageSize);
        var first = 0;

        while (true)
        {
            var path = $"/users?first={first}&max={pageSize}";

            using var response = await httpClient.GetAsync(path, cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                break;
            }
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var users = document.RootElement;
            if (users.ValueKind != JsonValueKind.Array)
            {
                break;
            }

            foreach (var element in users.EnumerateArray())
            {
                history.ProcessedCount++;

                var userId = element.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
                if (string.IsNullOrWhiteSpace(userId) || knownExternalIds.Contains(userId))
                {
                    continue;
                }

                orphanCandidates[userId] = new OrphanCandidate(
                    userId,
                    element.TryGetProperty("username", out var usernameElement) ? usernameElement.GetString() : null,
                    element.TryGetProperty("email", out var emailElement) ? emailElement.GetString() : null,
                    element.TryGetProperty("firstName", out var firstNameElement) && element.TryGetProperty("lastName", out var lastNameElement)
                        ? $"{firstNameElement.GetString()} {lastNameElement.GetString()}".Trim()
                        : null);
            }

            if (users.GetArrayLength() < pageSize)
            {
                break;
            }

            first += pageSize;
        }
    }

    private static async Task PersistOrphansAsync(
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
    }

    private static async Task ResolveSyncIssuesAsync(IdentityDbContext dbContext, CancellationToken cancellationToken)
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
    }

    private static async Task RecordSyncIssueAsync(IdentityDbContext dbContext, Exception ex, CancellationToken cancellationToken)
    {
        var issue = await dbContext.IdentitySyncIssues
            .Where(i => i.IssueType == IssueType && i.ResolvedUtc == null)
            .FirstOrDefaultAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        if (issue is null)
        {
            dbContext.IdentitySyncIssues.Add(new IdentitySyncIssue
            {
                IssueType = IssueType,
                Message = ex.Message,
                Details = ex.ToString(),
                CreatedUtc = now,
                LastOccurredUtc = now
            });
        }
        else
        {
            issue.Message = ex.Message;
            issue.Details = ex.ToString();
            issue.LastOccurredUtc = now;
        }
    }

    private sealed record OrphanCandidate(string UserId, string? Username, string? Email, string? DisplayName);
}
