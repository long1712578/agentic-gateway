using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgenticGateway.Infrastructure.Memory.Migrations;

[DbContext(typeof(MemoryDbContext))]
[Migration("202609260001_InitialMemory")]
public sealed class InitialMemory : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "memories",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ProjectId = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                Kind = table.Column<string>(type: "TEXT", maxLength: 24, nullable: false),
                Content = table.Column<string>(type: "TEXT", maxLength: 32000, nullable: false),
                SourceAgent = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                SourceSession = table.Column<string>(type: "TEXT", maxLength: 160, nullable: true),
                SourceReference = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                IdempotencyKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                CreatedAtUnixMs = table.Column<long>(type: "INTEGER", nullable: false),
                ExpiresAtUnixMs = table.Column<long>(type: "INTEGER", nullable: true),
                Revision = table.Column<long>(type: "INTEGER", nullable: false),
                IsDerived = table.Column<bool>(type: "INTEGER", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_memories", row => row.Id));

        migrationBuilder.CreateTable(
            name: "memory_dream_sources",
            columns: table => new
            {
                DreamMemoryId = table.Column<Guid>(type: "TEXT", nullable: false),
                SourceMemoryId = table.Column<Guid>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_memory_dream_sources", row => new { row.DreamMemoryId, row.SourceMemoryId });
                table.ForeignKey("FK_memory_dream_sources_memories_DreamMemoryId", row => row.DreamMemoryId,
                    "memories", "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_memory_dream_sources_memories_SourceMemoryId", row => row.SourceMemoryId,
                    "memories", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_memories_ProjectId_CreatedAtUnixMs", "memories", ["ProjectId", "CreatedAtUnixMs"]);
        migrationBuilder.CreateIndex("IX_memories_ProjectId_ExpiresAtUnixMs", "memories", ["ProjectId", "ExpiresAtUnixMs"]);
        migrationBuilder.CreateIndex("IX_memories_ProjectId_IdempotencyKey", "memories", ["ProjectId", "IdempotencyKey"], unique: true);
        migrationBuilder.CreateIndex("IX_memory_dream_sources_SourceMemoryId", "memory_dream_sources", "SourceMemoryId", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("memory_dream_sources");
        migrationBuilder.DropTable("memories");
    }
}
