using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetTracker.API.Migrations
{
    /// <inheritdoc />
    public partial class AddGlobalMerchantCategoryMappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GlobalMerchantCategoryMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MerchantName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CategoryName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ConfirmationCount = table.Column<int>(type: "integer", nullable: false),
                    ConfidenceScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlobalMerchantCategoryMappings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GlobalMerchantCategoryMappings_ConfidenceScore",
                table: "GlobalMerchantCategoryMappings",
                column: "ConfidenceScore");

            migrationBuilder.CreateIndex(
                name: "IX_GlobalMerchantCategoryMappings_MerchantName",
                table: "GlobalMerchantCategoryMappings",
                column: "MerchantName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GlobalMerchantCategoryMappings_Source",
                table: "GlobalMerchantCategoryMappings",
                column: "Source");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GlobalMerchantCategoryMappings");
        }
    }
}
