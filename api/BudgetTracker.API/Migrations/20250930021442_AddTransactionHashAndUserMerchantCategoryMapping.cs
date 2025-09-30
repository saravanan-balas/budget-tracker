using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace BudgetTracker.API.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionHashAndUserMerchantCategoryMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmbeddingCache");

            migrationBuilder.AddColumn<string>(
                name: "TransactionHash",
                table: "Transactions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserMerchantCategoryMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MerchantName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfidenceScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMerchantCategoryMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserMerchantCategoryMappings_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserMerchantCategoryMappings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_UserId_TransactionHash",
                table: "Transactions",
                columns: new[] { "UserId", "TransactionHash" });

            migrationBuilder.CreateIndex(
                name: "IX_UserMerchantCategoryMappings_CategoryId",
                table: "UserMerchantCategoryMappings",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_UserMerchantCategoryMappings_UserId_MerchantName",
                table: "UserMerchantCategoryMappings",
                columns: new[] { "UserId", "MerchantName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserMerchantCategoryMappings");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_UserId_TransactionHash",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "TransactionHash",
                table: "Transactions");

            migrationBuilder.CreateTable(
                name: "EmbeddingCache",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Embedding = table.Column<Vector>(type: "vector(1536)", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NormalizedText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    TextHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
    }
}
