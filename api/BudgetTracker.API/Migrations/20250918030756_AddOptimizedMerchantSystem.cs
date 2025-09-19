using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace BudgetTracker.API.Migrations
{
    /// <inheritdoc />
    public partial class AddOptimizedMerchantSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Vector>(
                name: "Embedding",
                table: "Merchants",
                type: "vector(1536)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EmbeddingCache",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NormalizedText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    TextHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Embedding = table.Column<Vector>(type: "vector(1536)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsageCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmbeddingCache", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmbeddingCache_TextHash",
                table: "EmbeddingCache",
                column: "TextHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmbeddingCache");

            migrationBuilder.DropColumn(
                name: "Embedding",
                table: "Merchants");
        }
    }
}
