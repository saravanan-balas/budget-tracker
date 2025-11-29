using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetTracker.API.Migrations
{
    /// <inheritdoc />
    public partial class FixApplicationLogsPropertiesColumnType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Change Properties column from text to jsonb
            migrationBuilder.Sql(@"
                ALTER TABLE ""ApplicationLogs"" 
                ALTER COLUMN ""Properties"" TYPE jsonb 
                USING CASE 
                    WHEN ""Properties"" IS NULL THEN NULL
                    WHEN ""Properties"" = '' THEN NULL
                    ELSE ""Properties""::jsonb
                END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert Properties column from jsonb to text
            migrationBuilder.Sql(@"
                ALTER TABLE ""ApplicationLogs"" 
                ALTER COLUMN ""Properties"" TYPE text 
                USING CASE 
                    WHEN ""Properties"" IS NULL THEN NULL
                    ELSE ""Properties""::text
                END;");
        }
    }
}
