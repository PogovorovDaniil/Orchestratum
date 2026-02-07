using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orchestratum.Example.Migrations
{
    /// <inheritdoc />
    public partial class InitialOrchestrator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "orchestrator_commands",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    executor = table.Column<string>(type: "text", nullable: false),
                    data_type = table.Column<string>(type: "text", nullable: false),
                    data = table.Column<string>(type: "text", nullable: false),
                    timeout = table.Column<TimeSpan>(type: "interval", nullable: false),
                    retries_left = table.Column<int>(type: "integer", nullable: false),
                    is_running = table.Column<bool>(type: "boolean", nullable: false),
                    run_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_completed = table.Column<bool>(type: "boolean", nullable: false),
                    complete_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_failed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orchestrator_commands", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_orchestrator_commands_is_completed",
                table: "orchestrator_commands",
                column: "is_completed");

            migrationBuilder.CreateIndex(
                name: "IX_orchestrator_commands_is_failed",
                table: "orchestrator_commands",
                column: "is_failed");

            migrationBuilder.CreateIndex(
                name: "IX_orchestrator_commands_is_running",
                table: "orchestrator_commands",
                column: "is_running");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "orchestrator_commands");
        }
    }
}
