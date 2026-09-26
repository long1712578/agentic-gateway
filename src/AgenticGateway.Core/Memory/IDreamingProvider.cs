namespace AgenticGateway.Core.Memory;

public interface IDreamingProvider
{
    Task<DreamProposal> ProposeSummaryAsync(
        string projectId,
        IReadOnlyList<MemoryEntry> sourceMemories,
        CancellationToken cancellationToken = default);
}
