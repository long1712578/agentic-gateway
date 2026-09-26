using AgenticGateway.Core.Memory;
using Microsoft.EntityFrameworkCore;

namespace AgenticGateway.Infrastructure.Memory;

public sealed class MemoryDbContext(DbContextOptions<MemoryDbContext> options) : DbContext(options)
{
    public DbSet<MemoryRow> Memories => Set<MemoryRow>();
    public DbSet<MemoryDreamSourceRow> DreamSources => Set<MemoryDreamSourceRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var memory = modelBuilder.Entity<MemoryRow>();
        memory.ToTable("memories");
        memory.HasKey(row => row.Id);
        memory.Property(row => row.ProjectId).HasMaxLength(120).IsRequired();
        memory.Property(row => row.Kind).HasConversion<string>().HasMaxLength(24).IsRequired();
        memory.Property(row => row.Content).HasMaxLength(32_000).IsRequired();
        memory.Property(row => row.SourceAgent).HasMaxLength(80).IsRequired();
        memory.Property(row => row.SourceSession).HasMaxLength(160);
        memory.Property(row => row.SourceReference).HasMaxLength(1_024);
        memory.Property(row => row.IdempotencyKey).HasMaxLength(200);
        memory.HasIndex(row => new { row.ProjectId, row.IdempotencyKey }).IsUnique();
        memory.HasIndex(row => new { row.ProjectId, row.CreatedAtUnixMs });
        memory.HasIndex(row => new { row.ProjectId, row.ExpiresAtUnixMs });

        var dreamSource = modelBuilder.Entity<MemoryDreamSourceRow>();
        dreamSource.ToTable("memory_dream_sources");
        dreamSource.HasKey(row => new { row.DreamMemoryId, row.SourceMemoryId });
        dreamSource.HasOne<MemoryRow>()
            .WithMany()
            .HasForeignKey(row => row.DreamMemoryId)
            .OnDelete(DeleteBehavior.Cascade);
        dreamSource.HasOne<MemoryRow>()
            .WithMany()
            .HasForeignKey(row => row.SourceMemoryId)
            .OnDelete(DeleteBehavior.Restrict);
        dreamSource.HasIndex(row => row.SourceMemoryId).IsUnique();
    }
}

public sealed class MemoryDreamSourceRow
{
    public Guid DreamMemoryId { get; set; }
    public Guid SourceMemoryId { get; set; }
}

public sealed class MemoryRow
{
    public Guid Id { get; set; }
    public string ProjectId { get; set; } = string.Empty;
    public MemoryKind Kind { get; set; }
    public string Content { get; set; } = string.Empty;
    public string SourceAgent { get; set; } = string.Empty;
    public string? SourceSession { get; set; }
    public string? SourceReference { get; set; }
    public string? IdempotencyKey { get; set; }
    public long CreatedAtUnixMs { get; set; }
    public long? ExpiresAtUnixMs { get; set; }
    public long Revision { get; set; }
    public bool IsDerived { get; set; }

    public MemoryEntry ToDomain() => new(
        Id, ProjectId, Kind, Content, SourceAgent, SourceSession, SourceReference,
        IdempotencyKey,
        DateTimeOffset.FromUnixTimeMilliseconds(CreatedAtUnixMs),
        ExpiresAtUnixMs is long expiresAt ? DateTimeOffset.FromUnixTimeMilliseconds(expiresAt) : null,
        Revision,
        IsDerived);
}
