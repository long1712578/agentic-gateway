namespace AgenticGateway.Core.Memory;

public sealed record RememberMemory(
    string ProjectId,
    MemoryKind Kind,
    string Content,
    string SourceAgent,
    string? SourceSession = null,
    string? SourceReference = null,
    string? IdempotencyKey = null,
    DateTimeOffset? ExpiresAt = null,
    bool IsDerived = false);
