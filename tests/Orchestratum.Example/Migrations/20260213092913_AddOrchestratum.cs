using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orchestratum.Example.Migrations
{
    /// <inheritdoc />
    public partial class AddOrchestratum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ORCH_commands",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    target = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    input = table.Column<string>(type: "text", nullable: true),
                    output = table.Column<string>(type: "text", nullable: true),
                    scheduled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    timeout = table.Column<TimeSpan>(type: "interval", nullable: false),
                    is_running = table.Column<bool>(type: "boolean", nullable: false),
                    running_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    run_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_completed = table.Column<bool>(type: "boolean", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_canceled = table.Column<bool>(type: "boolean", nullable: false),
                    canceled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    retries_left = table.Column<int>(type: "integer", nullable: false),
                    is_failed = table.Column<bool>(type: "boolean", nullable: false),
                    failed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ORCH_commands", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ORCH_commands_is_completed",
                table: "ORCH_commands",
                column: "is_completed");

            migrationBuilder.CreateIndex(
                name: "IX_ORCH_commands_is_failed",
                table: "ORCH_commands",
                column: "is_failed");

            migrationBuilder.CreateIndex(
                name: "IX_ORCH_commands_is_running",
                table: "ORCH_commands",
                column: "is_running");

            migrationBuilder.CreateIndex(
                name: "IX_ORCH_commands_target",
                table: "ORCH_commands",
                column: "target");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ORCH_commands");
        }
    }
}
