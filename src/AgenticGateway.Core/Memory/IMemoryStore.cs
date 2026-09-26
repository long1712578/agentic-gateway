namespace AgenticGateway.Core.Memory;

public interface IMemoryStore
{
    ValueTask<MemoryEntry> RememberAsync(RememberMemory memory, CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<MemorySearchResult>> SearchAsync(
        string projectId,
        string query,
        int limit,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<MemoryEntry>> GetDreamingCandidatesAsync(
        string projectId,
        int limit,
        CancellationToken cancellationToken = default);

    ValueTask<MemoryEntry> SaveDreamSummaryAsync(
        RememberMemory summary,
        IReadOnlyList<Guid> sourceMemoryIds,
        CancellationToken cancellationToken = default);
}
