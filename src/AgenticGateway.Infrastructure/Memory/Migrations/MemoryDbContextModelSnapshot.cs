using AgenticGateway.Core.Memory;
using AgenticGateway.Infrastructure.Memory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgenticGateway.Infrastructure.Memory.Migrations;

[DbContext(typeof(MemoryDbContext))]
public sealed class MemoryDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasAnnotation("ProductVersion", "10.0.12")
            .HasAnnotation("Relational:MaxIdentifierLength", 64);

        modelBuilder.Entity<MemoryRow>(entity =>
        {
            entity.ToTable("memories");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasColumnType("TEXT");
            entity.Property(row => row.ProjectId).HasMaxLength(120).IsRequired().HasColumnType("TEXT");
            entity.Property(row => row.Kind).HasConversion<string>().HasMaxLength(24).IsRequired().HasColumnType("TEXT");
            entity.Property(row => row.Content).HasMaxLength(32_000).IsRequired().HasColumnType("TEXT");
            entity.Property(row => row.SourceAgent).HasMaxLength(80).IsRequired().HasColumnType("TEXT");
            entity.Property(row => row.SourceSession).HasMaxLength(160).HasColumnType("TEXT");
            entity.Property(row => row.SourceReference).HasMaxLength(1_024).HasColumnType("TEXT");
            entity.Property(row => row.IdempotencyKey).HasMaxLength(200).HasColumnType("TEXT");
            entity.Property(row => row.CreatedAtUnixMs).HasColumnType("INTEGER");
            entity.Property(row => row.ExpiresAtUnixMs).HasColumnType("INTEGER");
            entity.Property(row => row.Revision).HasColumnType("INTEGER");
            entity.Property(row => row.IsDerived).HasColumnType("INTEGER");
            entity.HasIndex(row => new { row.ProjectId, row.CreatedAtUnixMs });
            entity.HasIndex(row => new { row.ProjectId, row.ExpiresAtUnixMs });
            entity.HasIndex(row => new { row.ProjectId, row.IdempotencyKey }).IsUnique();
        });

        modelBuilder.Entity<MemoryDreamSourceRow>(entity =>
        {
            entity.ToTable("memory_dream_sources");
            entity.HasKey(row => new { row.DreamMemoryId, row.SourceMemoryId });
            entity.Property(row => row.DreamMemoryId).HasColumnType("TEXT");
            entity.Property(row => row.SourceMemoryId).HasColumnType("TEXT");
            entity.HasIndex(row => row.SourceMemoryId).IsUnique();
            entity.HasOne<MemoryRow>().WithMany().HasForeignKey(row => row.DreamMemoryId)
                .OnDelete(DeleteBehavior.Cascade).IsRequired();
            entity.HasOne<MemoryRow>().WithMany().HasForeignKey(row => row.SourceMemoryId)
                .OnDelete(DeleteBehavior.Restrict).IsRequired();
        });
    }
}
