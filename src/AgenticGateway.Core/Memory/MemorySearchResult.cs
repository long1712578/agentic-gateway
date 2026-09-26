namespace AgenticGateway.Core.Memory;

public sealed record MemorySearchResult(MemoryEntry Entry, int Position, bool ContentTruncated = false);
