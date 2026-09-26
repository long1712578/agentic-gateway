namespace AgenticGateway.Core.Memory;

public sealed record DreamProposal(string Summary, IReadOnlyList<Guid> SourceMemoryIds);
