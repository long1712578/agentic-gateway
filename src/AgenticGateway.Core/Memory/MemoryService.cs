namespace AgenticGateway.Core.Memory;

public sealed class MemoryService(IMemoryStore store)
{
    private static readonly TimeSpan DefaultExpiry = TimeSpan.FromDays(90);
    private const int MaximumProjectIdLength = 120;
    private const int MaximumContentLength = 32_000;
    private const int MaximumQueryLength = 512;
    private const int MaximumRecallCharacters = 12_000;

    public ValueTask<MemoryEntry> RememberAsync(RememberMemory memory, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(memory);
        var projectId = Required(memory.ProjectId, nameof(memory.ProjectId), MaximumProjectIdLength);
        var content = Required(memory.Content, nameof(memory.Content), MaximumContentLength);
        var sourceAgent = Required(memory.SourceAgent, nameof(memory.SourceAgent), 80);
        var sourceSession = Optional(memory.SourceSession, 160);
        var sourceReference = Optional(memory.SourceReference, 1_024);
        var idempotencyKey = Optional(memory.IdempotencyKey, 200);
        if (!Enum.IsDefined(memory.Kind))
        {
            throw new ArgumentOutOfRangeException(nameof(memory.Kind));
        }
        var expiresAt = memory.ExpiresAt ?? DateTimeOffset.UtcNow.Add(DefaultExpiry);

        if (expiresAt <= DateTimeOffset.UtcNow)
        {
            throw new ArgumentOutOfRangeException(nameof(memory.ExpiresAt), "Memory expiry must be in the future.");
        }

        return store.RememberAsync(memory with
        {
            ProjectId = projectId,
            Content = content,
            SourceAgent = sourceAgent,
            SourceSession = sourceSession,
            SourceReference = sourceReference,
            IdempotencyKey = idempotencyKey,
            ExpiresAt = expiresAt
        }, cancellationToken);
    }

    public ValueTask<IReadOnlyList<MemorySearchResult>> RecallAsync(
        string projectId,
        string query,
        int limit = 5,
        CancellationToken cancellationToken = default)
    {
        projectId = Required(projectId, nameof(projectId), MaximumProjectIdLength);
        query = Required(query, nameof(query), MaximumQueryLength);
        if (limit is < 1 or > 8)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "Recall limit must be between 1 and 8.");
        }

        return BoundRecallAsync(projectId, query, limit, cancellationToken);
    }

    private async ValueTask<IReadOnlyList<MemorySearchResult>> BoundRecallAsync(
        string projectId,
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        var results = await store.SearchAsync(projectId, query, limit, cancellationToken);
        var bounded = new List<MemorySearchResult>(results.Count);
        var remaining = MaximumRecallCharacters;
        foreach (var result in results)
        {
            if (remaining <= 0)
            {
                break;
            }

            var entry = result.Entry;
            if (entry.Content.Length > remaining)
            {
                bounded.Add(result with
                {
                    Entry = entry with { Content = entry.Content[..remaining] },
                    ContentTruncated = true
                });
                break;
            }

            bounded.Add(result);
            remaining -= entry.Content.Length;
        }

        return bounded;
    }

    private static string Required(string? value, string name, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > maxLength)
        {
            throw new ArgumentException($"{name} must contain between 1 and {maxLength} characters.", name);
        }

        return normalized;
    }

    private static string? Optional(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        if (normalized is null)
        {
            return null;
        }

        if (normalized.Length == 0)
        {
            return null;
        }

        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"Value cannot exceed {maxLength} characters.", nameof(value));
        }

        return normalized;
    }
}
