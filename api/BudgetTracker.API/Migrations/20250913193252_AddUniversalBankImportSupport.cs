using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetTracker.API.Migrations
{
    /// <inheritdoc />
    public partial class AddUniversalBankImportSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AICost",
                table: "ImportedFiles",
                type: "numeric(10,4)",
                precision: 10,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DetectedBankName",
                table: "ImportedFiles",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DetectedCountry",
                table: "ImportedFiles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DetectedFormat",
                table: "ImportedFiles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsProcessedSynchronously",
                table: "ImportedFiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ParsingMetadata",
                table: "ImportedFiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemplateVersion",
                table: "ImportedFiles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BankTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BankName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FileFormat = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TemplatePattern = table.Column<string>(type: "text", nullable: false),
                    SuccessCount = table.Column<int>(type: "integer", nullable: false),
                    FailureCount = table.Column<int>(type: "integer", nullable: false),
                    ConfidenceScore = table.Column<double>(type: "double precision", nullable: false),
                    LastUsed = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImportParsingCache",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    BankName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ParsedStructure = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportParsingCache", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankTemplates_Lookup",
                table: "BankTemplates",
                columns: new[] { "BankName", "Country", "FileFormat" });

            migrationBuilder.CreateIndex(
                name: "IX_ImportParsingCache_FileHash",
                table: "ImportParsingCache",
                column: "FileHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BankTemplates");

            migrationBuilder.DropTable(
                name: "ImportParsingCache");

            migrationBuilder.DropColumn(
                name: "AICost",
                table: "ImportedFiles");

            migrationBuilder.DropColumn(
                name: "DetectedBankName",
                table: "ImportedFiles");

            migrationBuilder.DropColumn(
                name: "DetectedCountry",
                table: "ImportedFiles");

            migrationBuilder.DropColumn(
                name: "DetectedFormat",
                table: "ImportedFiles");

            migrationBuilder.DropColumn(
                name: "IsProcessedSynchronously",
                table: "ImportedFiles");

            migrationBuilder.DropColumn(
                name: "ParsingMetadata",
                table: "ImportedFiles");

            migrationBuilder.DropColumn(
                name: "TemplateVersion",
                table: "ImportedFiles");
        }
    }
}
