namespace AgenticGateway.Core.Memory;

public sealed record MemoryEntry(
    Guid Id,
    string ProjectId,
    MemoryKind Kind,
    string Content,
    string SourceAgent,
    string? SourceSession,
    string? SourceReference,
    string? IdempotencyKey,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    long Revision,
    bool IsDerived = false);
