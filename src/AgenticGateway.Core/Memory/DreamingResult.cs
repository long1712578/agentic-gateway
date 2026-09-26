namespace AgenticGateway.Core.Memory;

public sealed record DreamingResult(string Status, MemoryEntry? Summary, int SourceCount);
