using Microsoft.EntityFrameworkCore;

namespace AgenticGateway.Infrastructure.Memory;

public sealed class MemoryDatabaseInitializer(
    IDbContextFactory<MemoryDbContext> contextFactory)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.MigrateAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode = WAL;", cancellationToken);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE VIRTUAL TABLE IF NOT EXISTS memory_fts USING fts5(
                MemoryId UNINDEXED,
                ProjectId UNINDEXED,
                Content,
                tokenize = 'unicode61 remove_diacritics 2'
            );
            """, cancellationToken);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER IF NOT EXISTS memories_ai AFTER INSERT ON memories BEGIN
                INSERT INTO memory_fts (MemoryId, ProjectId, Content)
                VALUES (CAST(new.Id AS TEXT), new.ProjectId, new.Content);
            END;
            """, cancellationToken);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER IF NOT EXISTS memories_au AFTER UPDATE OF ProjectId, Content ON memories BEGIN
                DELETE FROM memory_fts WHERE MemoryId = CAST(old.Id AS TEXT);
                INSERT INTO memory_fts (MemoryId, ProjectId, Content)
                VALUES (CAST(new.Id AS TEXT), new.ProjectId, new.Content);
            END;
            """, cancellationToken);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER IF NOT EXISTS memories_ad AFTER DELETE ON memories BEGIN
                DELETE FROM memory_fts WHERE MemoryId = CAST(old.Id AS TEXT);
            END;
            """, cancellationToken);
        await db.Database.ExecuteSqlRawAsync("INSERT INTO memory_fts(memory_fts) VALUES ('rebuild');", cancellationToken);
    }

}
