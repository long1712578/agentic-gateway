using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AgenticGateway.Core.Memory;

public sealed class DreamingService(IMemoryStore store, IDreamingProvider provider)
{
    private const int BatchLimit = 20;
    private const int MaximumSummaryLength = 4_000;

    public async Task<DreamingResult> DreamAsync(string projectId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectId) || projectId.Trim().Length > 120)
        {
            throw new ArgumentException("projectId must contain between 1 and 120 characters.", nameof(projectId));
        }

        projectId = projectId.Trim();
        var source = await store.GetDreamingCandidatesAsync(projectId, BatchLimit, cancellationToken);
        if (source.Count < 2)
        {
            return new DreamingResult("not_enough_memories", null, source.Count);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var proposal = await provider.ProposeSummaryAsync(projectId, source, timeout.Token);
        var summary = proposal.Summary.Trim();
        if (summary.Length is 0 or > MaximumSummaryLength)
        {
            throw new InvalidOperationException($"Dream summary must contain between 1 and {MaximumSummaryLength} characters.");
        }

        var sourceIds = source.Select(entry => entry.Id).ToHashSet();
        var citedIds = proposal.SourceMemoryIds.Distinct().ToArray();
        if (citedIds.Length < 2 || citedIds.Any(id => !sourceIds.Contains(id)))
        {
            throw new InvalidOperationException("Dream summary must cite at least two source memories from its input batch.");
        }

        var orderedIds = citedIds.Order().ToArray();
        var idempotencyKey = CreateIdempotencyKey(projectId, orderedIds);
        var citedEntries = source.Where(entry => orderedIds.Contains(entry.Id)).ToArray();
        var expiry = citedEntries
            .Where(entry => entry.ExpiresAt is not null)
            .Select(entry => entry.ExpiresAt)
            .Min();
        var saved = await store.SaveDreamSummaryAsync(new RememberMemory(
            ProjectId: projectId,
            Kind: MemoryKind.Summary,
            Content: summary,
            SourceAgent: "dreaming",
            SourceReference: JsonSerializer.Serialize(orderedIds),
            IdempotencyKey: idempotencyKey,
            ExpiresAt: expiry,
            IsDerived: true), orderedIds, cancellationToken);

        return new DreamingResult("summarized", saved, citedIds.Length);
    }

    private static string CreateIdempotencyKey(string projectId, IReadOnlyList<Guid> sourceIds)
    {
        var canonical = $"{projectId}\n{string.Join('\n', sourceIds.Select(id => id.ToString("N")))}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        return $"dream:{hash}";
    }
}
