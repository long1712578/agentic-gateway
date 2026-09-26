using AgenticGateway.Core.Memory;
using Microsoft.EntityFrameworkCore;

namespace AgenticGateway.Infrastructure.Memory;

public sealed class SqliteMemoryStore(IDbContextFactory<MemoryDbContext> contextFactory) : IMemoryStore
{
    private readonly SemaphoreSlim _writeGate = new(1, 1);

    public async ValueTask<MemoryEntry> RememberAsync(RememberMemory memory, CancellationToken cancellationToken = default)
    {
        await _writeGate.WaitAsync(cancellationToken);
        try
        {
            return await RememberCoreAsync(memory, cancellationToken);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private async ValueTask<MemoryEntry> RememberCoreAsync(RememberMemory memory, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (memory.IdempotencyKey is not null)
        {
            var existing = await db.Memories
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    row => row.ProjectId == memory.ProjectId && row.IdempotencyKey == memory.IdempotencyKey,
                    cancellationToken);
            if (existing is not null)
            {
                if (existing.Kind != memory.Kind
                    || existing.Content != memory.Content
                    || existing.SourceAgent != memory.SourceAgent
                    || existing.SourceSession != memory.SourceSession
                    || existing.SourceReference != memory.SourceReference
                    || existing.IsDerived != memory.IsDerived)
                {
                    throw new InvalidOperationException("This idempotency key has already been used with different memory content.");
                }

                return existing.ToDomain();
            }
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var row = new MemoryRow
        {
            Id = Guid.NewGuid(),
            ProjectId = memory.ProjectId,
            Kind = memory.Kind,
            Content = memory.Content,
            SourceAgent = memory.SourceAgent,
            SourceSession = memory.SourceSession,
            SourceReference = memory.SourceReference,
            IdempotencyKey = memory.IdempotencyKey,
            CreatedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ExpiresAtUnixMs = memory.ExpiresAt?.ToUnixTimeMilliseconds(),
            Revision = 1,
            IsDerived = memory.IsDerived
        };

        db.Memories.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO memory_fts (MemoryId, ProjectId, Content)
            VALUES ({row.Id.ToString()}, {row.ProjectId}, {row.Content})
            """, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return row.ToDomain();
    }

    public async ValueTask<IReadOnlyList<MemorySearchResult>> SearchAsync(
        string projectId,
        string query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var terms = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(term => term.Trim('"', '\'', '*', '(', ')', ':', '^', '-', '+'))
            .Where(term => term.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(24)
            .ToArray();
        if (terms.Length == 0)
        {
            return [];
        }

        var expression = string.Join(" OR ", terms.Select(term => $"\"{term.Replace("\"", "\"\"", StringComparison.Ordinal)}\""));
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.Memories
            .FromSqlInterpolated($"""
                SELECT m.*
                FROM memories AS m
                INNER JOIN memory_fts AS f ON f.MemoryId = CAST(m.Id AS TEXT)
                WHERE memory_fts MATCH {expression}
                  AND f.ProjectId = {projectId}
                  AND m.ProjectId = {projectId}
                  AND (m.ExpiresAtUnixMs IS NULL OR m.ExpiresAtUnixMs > {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()})
                ORDER BY bm25(memory_fts), m.CreatedAtUnixMs DESC
                LIMIT {limit}
                """)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return rows.Select((row, index) => new MemorySearchResult(row.ToDomain(), index + 1)).ToArray();
    }

    public async ValueTask<IReadOnlyList<MemoryEntry>> GetDreamingCandidatesAsync(
        string projectId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var rows = await db.Memories
            .AsNoTracking()
            .Where(row => row.ProjectId == projectId
                && !row.IsDerived
                && !db.DreamSources.Any(link => link.SourceMemoryId == row.Id)
                && (row.ExpiresAtUnixMs == null || row.ExpiresAtUnixMs > now))
            .OrderBy(row => row.CreatedAtUnixMs)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return rows.Select(row => row.ToDomain()).ToArray();
    }

    public async ValueTask<MemoryEntry> SaveDreamSummaryAsync(
        RememberMemory summary,
        IReadOnlyList<Guid> sourceMemoryIds,
        CancellationToken cancellationToken = default)
    {
        await _writeGate.WaitAsync(cancellationToken);
        try
        {
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
            var existing = await db.Memories.AsNoTracking().SingleOrDefaultAsync(
                row => row.ProjectId == summary.ProjectId && row.IdempotencyKey == summary.IdempotencyKey,
                cancellationToken);
            if (existing is not null)
            {
                return existing.ToDomain();
            }

            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var row = new MemoryRow
            {
                Id = Guid.NewGuid(),
                ProjectId = summary.ProjectId,
                Kind = summary.Kind,
                Content = summary.Content,
                SourceAgent = summary.SourceAgent,
                SourceReference = summary.SourceReference,
                IdempotencyKey = summary.IdempotencyKey,
                CreatedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                ExpiresAtUnixMs = summary.ExpiresAt?.ToUnixTimeMilliseconds(),
                Revision = 1,
                IsDerived = true
            };
            db.Memories.Add(row);
            await db.SaveChangesAsync(cancellationToken);
            db.DreamSources.AddRange(sourceMemoryIds.Select(sourceId => new MemoryDreamSourceRow
            {
                DreamMemoryId = row.Id,
                SourceMemoryId = sourceId
            }));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return row.ToDomain();
        }
        finally
        {
            _writeGate.Release();
        }
    }
}
